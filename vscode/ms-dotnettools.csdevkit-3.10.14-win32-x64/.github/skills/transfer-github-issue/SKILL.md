# Transfer GitHub Issue

Transfers GitHub issues across organizations by creating a copy in the target repo, crediting the original author, cross-linking both issues, and closing the original.

## When to Use

-   User asks to transfer/move a GitHub issue to another repo
-   User wants to move an issue across GitHub organizations
-   GitHub's built-in transfer won't work (cross-org)

## Quick Start

```powershell
.\.github\skills\transfer-github-issue\transfer-github-issue.ps1 `
    -SourceRepo "owner/source-repo" `
    -IssueNumber <number> `
    -TargetRepo "owner/target-repo"
```

### Dry Run (Preview)

```powershell
.\.github\skills\transfer-github-issue\transfer-github-issue.ps1 `
    -SourceRepo "dotnet/vscode-csharp" `
    -IssueNumber 9000 `
    -TargetRepo "microsoft/vscode-dotnettools" `
    -DryRun
```

## What It Does

1. **Reads** the source issue (title, body, labels)
2. **Creates** a new issue in the target repo with attribution to the original filer
3. **Copies** all comments (with author attribution)
4. **Cross-links** both issues so they reference each other
5. **Closes** the source issue with a pointer to the new location

## Parameters

| Parameter      | Required | Default | Description                                       |
| -------------- | -------- | ------- | ------------------------------------------------- |
| `-SourceRepo`  | Yes      |         | Source repo in `owner/repo` format                |
| `-IssueNumber` | Yes      |         | Issue number to transfer                          |
| `-TargetRepo`  | Yes      |         | Target repo in `owner/repo` format                |
| `-GitHubToken` | No       |         | GitHub PAT (falls back to `GH_TOKEN` / `gh auth`) |
| `-DryRun`      | No       | `false` | Preview what would happen without making changes  |
| `-CopyLabels`  | No       | `false` | Copy matching labels to the target repo           |

## Authentication

The script looks for a token in this order:

1. `-GitHubToken` parameter
2. `GH_TOKEN` environment variable
3. `GITHUB_TOKEN` environment variable
4. `gh auth token` (GitHub CLI)
5. **Interactive prompt** — if none of the above are found, the script walks the user through creating a PAT and lets them paste it directly

### Creating a PAT

If no token is found, the script prints step-by-step instructions for creating one:

-   **Option 1: GitHub CLI** — `gh auth login` (recommended, no manual scoping needed)
-   **Option 2: Fine-grained PAT** — scoped to just the two repos with Issues read/write
-   **Option 3: Classic PAT** — with `public_repo` or `repo` scope

The user can also paste a token at the interactive prompt to continue immediately without restarting the script.

If an API call fails with **401** (expired/invalid token) or **403** (insufficient scopes), the script prints specific remediation steps with direct links to the token settings pages.

### Required PAT Scopes

| PAT Type     | Permission Needed                                      |
| ------------ | ------------------------------------------------------ |
| Fine-grained | Issues → Read and write (on both source & target repo) |
| Classic      | `public_repo` (public repos) or `repo` (private repos) |

## What the Transferred Issue Looks Like

The new issue includes an attribution block at the top:

> **Note:** This issue was transferred from https://github.com/dotnet/vscode-csharp/issues/9000
> Originally filed by @JohnGalt1717 on 2026-02-12

---

_(original issue body follows)_

## Workflow for Agents

1. **Parse the request** — extract source repo, issue number, and target repo
2. **Run a dry run first** if unsure — add `-DryRun`
3. **Execute the transfer** — run without `-DryRun`
4. **Report the results** — share the new issue URL with the user

## Troubleshooting

| Error                     | What Happens                                                                  |
| ------------------------- | ----------------------------------------------------------------------------- |
| No token found            | Script prints PAT creation walkthrough and prompts to paste a token           |
| HTTP 401                  | Script prints how to regenerate an expired/invalid token with links           |
| HTTP 403                  | Script prints exact scopes needed for fine-grained and classic PATs           |
| HTTP 404                  | Script explains: wrong repo name, private repo without access, or bad issue # |
| HTTP 422                  | Issue body too long (>65536 chars) or missing field                           |
| "Issue is a pull request" | Script only transfers issues, not PRs                                         |
