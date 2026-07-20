<#
.SYNOPSIS
    Transfers a GitHub issue across organizations/repos by creating a copy in the
    target repo, crediting the original author, cross-linking both issues, and
    closing the original.

.DESCRIPTION
    GitHub's built-in transfer only works within a single organization. This script
    handles cross-org transfers by:
      1. Reading the source issue (title, body, labels)
      2. Creating a new issue in the target repo with attribution to the original filer
      3. Copying over comments (with attribution)
      4. Adding cross-reference links in both issues
      5. Closing the source issue with a pointer to the new one

.PARAMETER SourceRepo
    Source repository in "owner/repo" format (e.g. "dotnet/vscode-csharp")

.PARAMETER IssueNumber
    The issue number to transfer

.PARAMETER TargetRepo
    Target repository in "owner/repo" format (e.g. "microsoft/vscode-dotnettools")

.PARAMETER GitHubToken
    A GitHub PAT with repo scope. If not provided, falls back to the GH_TOKEN or
    GITHUB_TOKEN environment variable, or prompts interactively.

.PARAMETER DryRun
    If set, prints what would happen without making any changes.

.PARAMETER CopyLabels
    If set, attempts to apply matching labels from the source issue to the target.
    Labels that don't exist in the target repo are silently skipped. Default: $false.

.EXAMPLE
    .\transfer-github-issue.ps1 -SourceRepo "dotnet/vscode-csharp" -IssueNumber 9000 -TargetRepo "microsoft/vscode-dotnettools"

.EXAMPLE
    .\transfer-github-issue.ps1 -SourceRepo "dotnet/vscode-csharp" -IssueNumber 9000 -TargetRepo "microsoft/vscode-dotnettools" -DryRun

.NOTES
    Requires a GitHub PAT with the following scopes:
      - repo (to read/write issues in both public and private repos)
    For public repos only, "public_repo" scope is sufficient.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$SourceRepo,

    [Parameter(Mandatory = $true)]
    [int]$IssueNumber,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$TargetRepo,

    [Parameter(Mandatory = $false)]
    [string]$GitHubToken,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$CopyLabels
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve token ────────────────────────────────────────────────────────────
if (-not $GitHubToken) {
    $GitHubToken = $env:GH_TOKEN
}
if (-not $GitHubToken) {
    $GitHubToken = $env:GITHUB_TOKEN
}
if (-not $GitHubToken) {
    # Try gh CLI auth token
    try {
        $GitHubToken = (gh auth token 2>$null)
    }
    catch { }
}
if (-not $GitHubToken) {
    Write-Host ""
    Write-Host "=== No GitHub Token Found ===" -ForegroundColor Yellow
    Write-Host "This script needs a GitHub Personal Access Token (PAT) to read and write issues."
    Write-Host ""
    Write-Host "You have two options:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Option 1: GitHub CLI (recommended)" -ForegroundColor Green
    Write-Host "    Run:  gh auth login"
    Write-Host "    Then re-run this script."
    Write-Host ""
    Write-Host "  Option 2: Create a Fine-Grained PAT" -ForegroundColor Green
    Write-Host "    1. Go to: https://github.com/settings/personal-access-tokens/new"
    Write-Host "    2. Token name: issue-transfer (or any name you like)"
    Write-Host "    3. Expiration: 7 days (short-lived is best)"
    Write-Host "    4. Resource owner: Select your personal account"
    Write-Host "       (Fine-grained PATs scoped to an org need org admin approval)"
    Write-Host "    5. Repository access: 'Only select repositories'"
    Write-Host "       -> Pick BOTH: $SourceRepo and $TargetRepo"
    Write-Host "    6. Permissions -> Repository permissions:"
    Write-Host "       -> Issues: Read and write" -ForegroundColor White
    Write-Host "       (That's the ONLY permission needed)"
    Write-Host "    7. Click 'Generate token' and copy it"
    Write-Host ""
    Write-Host "  Option 3: Create a Classic PAT" -ForegroundColor Green
    Write-Host "    1. Go to: https://github.com/settings/tokens/new"
    Write-Host "    2. Token name: issue-transfer"
    Write-Host "    3. Expiration: 7 days"
    Write-Host "    4. Scopes: check 'public_repo' (for public repos)"
    Write-Host "       or check 'repo' (if either repo is private)"
    Write-Host "    5. Click 'Generate token' and copy it"
    Write-Host ""
    Write-Host "Once you have a token, either:" -ForegroundColor Cyan
    Write-Host '  $env:GH_TOKEN = "ghp_your_token_here"'
    Write-Host "  Then re-run this script."
    Write-Host ""
    Write-Host "  Or pass it directly:" -ForegroundColor Cyan
    Write-Host "  .\transfer-github-issue.ps1 ... -GitHubToken `"ghp_your_token_here`""
    Write-Host ""

    # Interactive prompt: let the user paste a token right now
    $inputToken = Read-Host "Or paste your token now to continue (Enter to abort)"
    if ([string]::IsNullOrWhiteSpace($inputToken)) {
        Write-Host "Aborted. No token provided." -ForegroundColor Red
        exit 1
    }
    $GitHubToken = $inputToken.Trim()
    Write-Host "  Token accepted. Continuing..." -ForegroundColor Green
}

$headers = @{
    Authorization  = "Bearer $GitHubToken"
    Accept         = 'application/vnd.github+json'
    'User-Agent'   = 'transfer-github-issue-skill/1.0'
    'X-GitHub-Api-Version' = '2022-11-28'
}

# ── Helper: call GitHub API ──────────────────────────────────────────────────
function Invoke-GitHubApi {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body
    )
    $params = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = $headers
        ContentType = 'application/json; charset=utf-8'
    }
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }
    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        $detail = $_.ErrorDetails.Message
        Write-Host "`nGitHub API error ($Method $Uri): HTTP $status" -ForegroundColor Red
        if ($detail) { Write-Host "  $detail" -ForegroundColor Red }

        if ($status -eq 401) {
            Write-Host ""
            Write-Host "=== Authentication Failed (401) ===" -ForegroundColor Yellow
            Write-Host "Your token is invalid or expired."
            Write-Host ""
            Write-Host "To fix:" -ForegroundColor Cyan
            Write-Host "  1. Go to https://github.com/settings/tokens"
            Write-Host "  2. Check if your token is expired and regenerate it"
            Write-Host "  3. Update: `$env:GH_TOKEN = `"ghp_new_token`""
        }
        elseif ($status -eq 403) {
            Write-Host ""
            Write-Host "=== Insufficient Permissions (403) ===" -ForegroundColor Yellow
            Write-Host "Your token doesn't have the required scopes."
            Write-Host ""
            Write-Host "Required permissions:" -ForegroundColor Cyan
            Write-Host "  Fine-grained PAT: Issues -> Read and write (on both repos)"
            Write-Host "  Classic PAT:      'public_repo' (public) or 'repo' (private)"
            Write-Host ""
            Write-Host "To create a new token with correct scopes:" -ForegroundColor Cyan
            Write-Host "  Fine-grained: https://github.com/settings/personal-access-tokens/new"
            Write-Host "  Classic:      https://github.com/settings/tokens/new"
        }
        elseif ($status -eq 404) {
            Write-Host ""
            Write-Host "=== Not Found (404) ===" -ForegroundColor Yellow
            Write-Host "The repository or issue was not found. This can mean:"
            Write-Host "  - The repo name is wrong (expected format: owner/repo)"
            Write-Host "  - The repo is private and your token lacks access"
            Write-Host "  - The issue number doesn't exist"
        }

        throw
    }
}

# ── Step 1: Fetch source issue ───────────────────────────────────────────────
Write-Host "`n=== Step 1: Fetching source issue ===" -ForegroundColor Cyan
$sourceUrl = "https://api.github.com/repos/$SourceRepo/issues/$IssueNumber"
$sourceIssue = Invoke-GitHubApi -Method GET -Uri $sourceUrl

if ($sourceIssue.PSObject.Properties['pull_request']) {
    Write-Error "Issue #$IssueNumber is a pull request, not an issue. Aborting."
    exit 1
}
if ($sourceIssue.state -eq 'closed') {
    Write-Warning "Source issue #$IssueNumber is already closed. Continuing anyway..."
}

$originalAuthor = $sourceIssue.user.login
$originalUrl = $sourceIssue.html_url
$originalTitle = $sourceIssue.title
$originalBody = $sourceIssue.body
$originalLabels = @($sourceIssue.labels | ForEach-Object { $_.name })

Write-Host "  Source: $originalUrl"
Write-Host "  Title:  $originalTitle"
Write-Host "  Author: @$originalAuthor"
Write-Host "  Labels: $($originalLabels -join ', ')"

# ── Step 2: Fetch comments from source issue ─────────────────────────────────
Write-Host "`n=== Step 2: Fetching comments ===" -ForegroundColor Cyan
$commentsUrl = "https://api.github.com/repos/$SourceRepo/issues/$IssueNumber/comments?per_page=100"
$sourceComments = @(Invoke-GitHubApi -Method GET -Uri $commentsUrl)
Write-Host "  Found $($sourceComments.Count) comment(s)"

# ── Step 3: Build the new issue body with attribution ────────────────────────
Write-Host "`n=== Step 3: Creating issue in target repo ===" -ForegroundColor Cyan

$attribution = @"
> **Note:** This issue was transferred from $originalUrl
> Originally filed by @$originalAuthor on $([datetime]::Parse($sourceIssue.created_at).ToString('yyyy-MM-dd'))

---

"@

$newBody = $attribution + $originalBody

if ($DryRun) {
    Write-Host "  [DRY RUN] Would create issue in $TargetRepo" -ForegroundColor Yellow
    Write-Host "  Title: $originalTitle"
    Write-Host "  Body length: $($newBody.Length) chars"
}
else {
    $createBody = @{
        title = $originalTitle
        body  = $newBody
    }

    # Optionally copy labels
    if ($CopyLabels -and $originalLabels.Count -gt 0) {
        # Fetch target repo labels to see which ones exist
        $targetLabelsUrl = "https://api.github.com/repos/$TargetRepo/labels?per_page=100"
        try {
            $targetLabels = @(Invoke-GitHubApi -Method GET -Uri $targetLabelsUrl)
            $targetLabelNames = @($targetLabels | ForEach-Object { $_.name })
            $matchingLabels = @($originalLabels | Where-Object { $targetLabelNames -contains $_ })
            if ($matchingLabels.Count -gt 0) {
                $createBody.labels = $matchingLabels
                Write-Host "  Copying labels: $($matchingLabels -join ', ')"
            }
            $skippedLabels = @($originalLabels | Where-Object { $targetLabelNames -notcontains $_ })
            if ($skippedLabels.Count -gt 0) {
                Write-Host "  Skipped labels (not in target): $($skippedLabels -join ', ')" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Warning "Could not fetch target repo labels. Skipping label copy."
        }
    }

    $createUrl = "https://api.github.com/repos/$TargetRepo/issues"
    $newIssue = Invoke-GitHubApi -Method POST -Uri $createUrl -Body $createBody

    $newIssueNumber = $newIssue.number
    $newIssueUrl = $newIssue.html_url
    Write-Host "  Created: $newIssueUrl" -ForegroundColor Green
}

# ── Step 4: Copy comments with attribution ───────────────────────────────────
if ($sourceComments.Count -gt 0) {
    Write-Host "`n=== Step 4: Copying comments ===" -ForegroundColor Cyan

    foreach ($comment in $sourceComments) {
        $commentAuthor = $comment.user.login
        $commentDate = [datetime]::Parse($comment.created_at).ToString('yyyy-MM-dd')
        $commentAttribution = "> **Comment by @$commentAuthor** on $commentDate`n`n"
        $commentBody = $commentAttribution + $comment.body

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would copy comment by @$commentAuthor ($commentDate)" -ForegroundColor Yellow
        }
        else {
            $commentUrl = "https://api.github.com/repos/$TargetRepo/issues/$newIssueNumber/comments"
            Invoke-GitHubApi -Method POST -Uri $commentUrl -Body @{ body = $commentBody } | Out-Null
            Write-Host "  Copied comment by @$commentAuthor ($commentDate)"
        }
    }
}
else {
    Write-Host "`n=== Step 4: No comments to copy ===" -ForegroundColor Cyan
}

# ── Step 5: Add cross-reference link on the new issue ────────────────────────
Write-Host "`n=== Step 5: Adding cross-reference links ===" -ForegroundColor Cyan

if (-not $DryRun) {
    # Comment on the new issue pointing back to original
    $backRefBody = "Transferred from $originalUrl"
    $backRefUrl = "https://api.github.com/repos/$TargetRepo/issues/$newIssueNumber/comments"
    Invoke-GitHubApi -Method POST -Uri $backRefUrl -Body @{ body = $backRefBody } | Out-Null
    Write-Host "  Added back-reference on new issue"
}

# ── Step 6: Close the source issue with a pointer ────────────────────────────
Write-Host "`n=== Step 6: Closing source issue ===" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "  [DRY RUN] Would comment on and close $SourceRepo#$IssueNumber" -ForegroundColor Yellow
}
else {
    # Add comment pointing to the new issue
    $closeComment = "This issue has been transferred to $newIssueUrl`n`nAll future discussion should happen there. Thank you!"
    $closeCommentUrl = "https://api.github.com/repos/$SourceRepo/issues/$IssueNumber/comments"
    Invoke-GitHubApi -Method POST -Uri $closeCommentUrl -Body @{ body = $closeComment } | Out-Null
    Write-Host "  Added transfer comment on source issue"

    # Close the source issue
    $closeUrl = "https://api.github.com/repos/$SourceRepo/issues/$IssueNumber"
    Invoke-GitHubApi -Method PATCH -Uri $closeUrl -Body @{
        state        = 'closed'
        state_reason = 'not_planned'
    } | Out-Null
    Write-Host "  Closed source issue #$IssueNumber" -ForegroundColor Green
}

# ── Summary ──────────────────────────────────────────────────────────────────
Write-Host "`n=== Transfer Complete ===" -ForegroundColor Green
if ($DryRun) {
    Write-Host "  [DRY RUN] No changes were made." -ForegroundColor Yellow
}
else {
    Write-Host "  Source: $originalUrl (CLOSED)"
    Write-Host "  Target: $newIssueUrl (OPEN)"
    Write-Host "  Original author: @$originalAuthor (credited in issue body)"
    Write-Host "  Comments copied: $($sourceComments.Count)"
}
