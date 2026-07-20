# File-Based App (FBA) Detection

## Problem Summary
We need to determine when a C# file should be treated as a File-Based App vs a Project-Based App vs a Misc File, and make this status visible to users and debuggable for the team.

## Context
With the introduction of File-Based Apps (top-level statements without explicit project files), we need clear heuristics to determine how to treat each file. This affects:
- IntelliSense behavior
- Debugging functionality
- User feedback and bug reports (need to know what mode they're in)
- Integration with the file-not-in-solution notification feature ([PR 687403](https://devdiv.visualstudio.com/DevDiv/_git/vs-green/pullrequest/687403))

The classification needs to work across different workspace scenarios:
- Small workspaces with few files
- Large repos like Picasso (hundreds of projects, uses Aspire, exclusively uses C# Dev Kit)
- Aspire app hosts (planning to use FBAs)
- Workspace-Based Development (WBD) mode

## Problem Details

### Current Classification Logic

This is the decision tree for determining how to classify a C# file. This is accurate as of [vscode-csharp 2.103.33-prerelease](https://github.com/dotnet/vscode-csharp/releases/tag/v2.103.33-prerelease).

Possible Classifications:
   - Project-Based App: C# file is associated with an ordinary .csproj. Standard set of editor features are present.
   - Misc file: C# file is not associated with any project, and has minimal/no editor features (e.g. no semantic diagnostics).
       - This designation is frequently used when projects are not fully loaded, in order to provide the editor immediate information about the file.
   - File-based app: C# file is the entry point of a "file-based app" project. Has standard set of .NET Core references as well as customization given by `#:` directives. Has semantic diagnostics and many standard editor features.

1. **Is the file in a currently loaded project?**
    - **Yes** → Classify as **Project-Based App**
    - **No** → Continue to next check
    - **NOTE:** When a project (re)load completes, we revisit this classification, so that files may start being treated as project-based app files, when they were previously classified as something else.

2. **Is `enableFileBasedPrograms` enabled?** (default: `true` in release)
    - **No** → Classify as **Misc File**.
    - **Yes** → Continue to next check

3. **Does the file have `#:` or `#!` directives?**
    - **Yes** → Classify as **File-Based App**
    - **No** → Continue to next check

4. **Is `enableFileBasedProgramsWhenAmbiguous` enabled?** (default: `false` in release)
   - **No** → Classify as **Misc File** (wait for project to load)
   - **Yes** → Continue to heuristic detection

**Heuristic Detection (when `enableFileBasedProgramsWhenAmbiguous: true`):**

5. **Are top-level statements present?**
   - **Yes** → Classify as **File-Based App**
   - **No** → Classify as **Misc File**

### Proposed Future Classification Logic

This is the proposed future decision tree for determining how to classify a C# file.

The goal of these changes is to improve accuracy of the heuristics, and gauge impact of those changes, such that we can consider eventually moving certain heuristic steps "above" the `enableFileBasedProgramsWhenAmbiguous` flag check in the decision tree.

We intend to implement this in a future version of the Roslyn Language Server (think Jan-Feb 2026).

This is the proposed decision tree for determining how to classify a C# file:

1. **Is the file in a currently loaded project?**
   - **Yes** → Classify as **Project-Based App**
   - **No** → Continue to next check

2. **Is `enableFileBasedPrograms` enabled?** (default: `true` in release)
   - **No** → Classify as **Misc File**
   - **Yes** → Continue to next check

3. **Does the file have `#:` or `#!` directives?**
   - **Yes** → Classify as **File-Based App**
   - **No** → Continue to next check

4. **Is `enableFileBasedProgramsWhenAmbiguous` enabled?** (default: `false` in release)
   - **No** → Classify as **Misc File**
   - **Yes** → Continue to heuristic detection

**Heuristic Detection (when `enableFileBasedProgramsWhenAmbiguous: true`):**

5. **Is the file included in a `.csproj` cone?**
   - "Cone" means that a containing directory, at some level of nesting, has a `.csproj` file in it.
   - **Yes** → Classify as **Misc File** (wait for project to load)
   - **No** → Continue to next check

6. **Are top-level statements present?**
   - **Yes** → Classify as **File-Based App**
   - **No** → Classify as **Misc File**

### Feature Flags

**`enableFileBasedPrograms`** (default: `true` in release)
- Controls overall File-Based App support
- When enabled, files with `#:`/`#:!` directives are always treated as FBAs

**`enableFileBasedProgramsWhenAmbiguous`** (default: `false` in release)
- Controls heuristic-based FBA classification (steps 5-6 in the proposed future classification and step 5 in the current classification above)
- When disabled, files without top-level statements won't show "phantom diagnostics" before project loading completes
- Users can opt-in to enable heuristic detection while we refine the classification logic

### User Experience

1. **Status Bar Visibility**: Show the current file's mode in the status bar
   - "File Based App: something.cs"
   - "Project: Something.csproj"
   - "Misc File: Something.cs"

2. **Mode Switching**: Allow users to manually override classification (with persistence in workspace settings)

3. **Integration with File-Not-In-Solution Feature**: Work harmoniously with PR 687403 to ensure correct project configuration

## Solutions Considered and Rejected

- Relying on file based apps having directives
- Custom file extension
- Recommending naming conventions *.app.cs

## Open Questions

### Technical Implementation

1. **"Is the file in a project that is not in the solution?"**
   - This is difficult to answer correctly
   - Could guess by walking up the directory tree looking for .csproj
   - But projects can reference files from anywhere (not just in their directory tree)
   - Loading projects implicitly to check has perf impact
   - Less likely in Workspace-Based Development (unless project is outside open folder)
   - Who is the authority for answering this question?
   - Need a single implementation that can be queried

2. **"Is the project system up-to-date for file?"**
   - Project system should be the authority on this
   - Need a way to query this state

### User Conventions and Configuration

3. **Naming conventions**: Is there a recommended pattern we should encourage?
   - Any existing trends in how folks name their File-Based Apps?
   - **Damian's recommendation w/o actually recommending**: Use `*.app.cs` pattern (e.g., `Program.app.cs`, `MyScript.app.cs`)
   - Could we start a convention to make detection more reliable?
   - Could this support explicit marking (editor command, workspace setting)?

4. **Directory conventions**: Where should FBAs be allowed?
   - Rikki's proposal: Assume SDK-style default behavior (EnableDefaultCompileItems, no explicit Compile items)
   - **Recommendation**: Don't put FBAs in the same directory as a .csproj file
   - If you want to exclude a file using `Compile Exclude`, experience will be clunky
   - "Embrace the platform conventions and the tooling will work better"

5. **Workspace configuration**: Should large repos be able to opt-out or configure FBA behavior?
   - Globbing patterns to declare where to watch for FBAs?
   - Setting to disable FBA detection in very large repos?


## Meeting Notes

### December 8, 2025 - Hard Problem Discussion

**Attendees**: Jake Radzikowski, Rikki Gibson, Damian Edwards, Jared Parsons, Drew Noakes, Lifeng Lu, Jason Malinowski, David Barbet

**Duration**: ~56 minutes

**Outcome**: Consensus on initial heuristic approach and strong recommendation for directory conventions.

#### Meeting Flow

The discussion progressed through several key realizations:

1. **Initial problem**: Files with only top-level statements (no directives) create ambiguity
2. **Temporal issues**: Load state, renames, and project initialization cause classification to change over time
3. **Heuristic proposal**: Use project cone (file system walk) as fast, reliable indicator
4. **Convention emergence**: Strong recommendation to separate FBAs from project folders
5. **Future-proofing**: Explicit directives for multi-file FBAs will simplify detection

The team moved from uncertainty about heuristics to consensus on a simple, explainable approach.

#### Key Decisions

**Initial Heuristic (Agreed Upon)**:
1. If file has `#:` directive → FBA
2. Else, if file is in project cone → Project file
3. Else → FBA until proven otherwise

**Strong Recommendation**: Avoid placing FBAs in project folders for better tooling experience.

#### Discussion Summary

**Problem Framing** (Jake Radzikowski)
- Current editor logic uses a "preference hierarchy" that favors project context over FBA
- Transitional states (initial load, renames) cause temporary misclassification, leading to bogus semantic errors
- No API exists to query a file's real-time load state confidently
- Need reliable heuristics and conventions for editor behavior when files have ambiguous context

**Heuristic Detection Proposal** (Rikki Gibson)
- **Directive-based detection**: If file contains `#!` or `#:` directives → treat as FBA immediately
- **Project cone check**: If no directives, walk up directories:
  - If `.csproj` found → treat as project file
  - If not → treat as FBA until proven otherwise
- **Fallback**: Use `IProjectInitializationStatusService` for definitive answers when available
- Emphasized need for flexibility as the feature evolves

**Feature Flags** (Team Discussion)
- `enableFileBasedApps` (defaults to true)
- Experimental flag for ambiguous cases (top-level statements without directives)
- Naming suggestions discussed: `EnableFileBasedAppAutoDetectionExperimental` or `EnableAmbiguousFileBasedApps`
- **Consensus**: Keep flags for now, remove when heuristics are reliable

**Performance & UX Concerns** (Lifeng Lu, Jared Parsons, Jake Radzikowski)
- Waiting for design-time build (DTB) is slow for large repos
- Heuristics should prioritize fast detection for common cases
- **Recommendations for fast detection**:
  - Avoid placing FBAs in the same folder as `.csproj`
  - Naming patterns like `*.app.cs` were debated
  - Project cone check is fast (file system walk, no MSBuild eval required)
- **Large repo optimizations**:
  - Globbing patterns to declare where to watch for FBAs (Lifeng)
  - Option to disable FBA feature entirely in very large repos (Lifeng)
  - Expectation: FBAs more likely in small workspaces
  - Edge case: "Unless the project is outside of the folder that the user has open" (Jake)
- Better to get quick answer for majority of cases than wait for slow accurate answer

**Edge Cases Discussed**:
- **Broken projects**: If `.csproj` has typos or evaluation errors, files might incorrectly appear as FBAs
- **XAML-generated files**: Files generated during build might need special handling (likely exclude their folders)
- **Linked entry points**: Files with top-level statements linked from outside project cone might temporarily show as FBA
  - Rare scenario, temporal dependency accepted
- **Directive ordering**: When file has directives AND is in loaded project, loaded project wins (shows directive errors)

**Naming Conventions Debate** (Damian Edwards, Jared Parsons, Jake Radzikowski)
- Jake's suggestions for custom conventions:
  - Patterns like `fileBased_*.cs` or `*.app.cs`
  - Templates could use pattern by default
  - Roslyn analyzers could provide warnings
- **Damian's position**: Strongly opposed mandatory naming conventions
  - Opt-in patterns acceptable (e.g., setting: `"Use this pattern to detect file-based apps: *.app.cs"`)
  - But recommendations "add complexity to something we explicitly wanted to remove concepts from"
- **Jared's position**: Supported conventions for better UX, warned against excessive settings
- **Resolution**: Opt-in patterns via settings considered acceptable, but resist urge to add checkboxes

**Context Switching & Excluded Files** (Drew Noakes, Jason Malinowski, David Barbet)
- Should excluded files be treated as FBAs?
- Suggestion to show file mode (FBA/project/misc) in status bar
- Need for explicit override options (editor command or workspace setting)
- VS Code may need context-switching UX (similar to VS) for files linked into multiple projects or FBAs
- **Current VS Code behavior**: When a file is in multiple projects, one wins (arbitrary but consistent after load)
- **Open issue**: [Implement a project context picker / choose active context (TFM) #5788](https://github.com/dotnet/vscode-csharp/issues/5788)

**Status Bar Integration** (Lifeng Lu, Jake Radzikowski)
- Currently shows project of active file in status bar (Lifeng)
- Proposal: Extend to show FBA/misc file status
- Natural location for in-place command to switch between misc/FBA status (Lifeng)
- Settings could be persisted in workspace configuration (Jake)

**Excluded Files Discussion** (Jake Radzikowski, Lifeng Lu)
- Question: Should excluded files be considered FBAs? (Jake)
- Potential downside: Users may see more error messages (Lifeng)
- **Assessment**: Not a big deal if it doesn't cause other side effects (Lifeng)

**Solution Filter Files (SLNF)** (Jake Radzikowski)
- Question raised: Should FBAs work even when using `.slnf` files?
- **Consensus**: Yes, FBAs should provide IntelliSense even if a project is excluded via solution filter
- FBAs operate independently of solution/project context

**Future Considerations** (Damian Edwards)
- Multi-file FBAs will likely use **explicit directives** for inclusion (not implicit globs)
- Two new directive types planned:
  - **File inclusion**: Like `Compile Include` in projects
  - **FBA-to-FBA references**: Similar to project-to-project references (enables unit testing, benchmarking)
- Implicit multi-file considered wrong choice based on user feedback
- New directives will make heuristic detection easier (no MSBuild eval needed)

**Convention Philosophy** (Jared Parsons, Damian Edwards, Lifeng Lu)
- **Jared's position**: Tell users "here are the conventions for the best experience" - similar to Go's `_test.go` pattern
  - Quote: *"Are we OK basically telling people like if you want the best editor experience, here are the conventions you need to follow in your repository. ... if we just tell people like listen if you want the good out-of-the-box experience like here's. That's how you do your repo. But you know, if you want to do something insane, like, yeah, we might take a bit of a detour getting you there."*
- **Damian's caution**: Conventions must be decided carefully and teams must be willing to break them based on feedback
  - Quote: *"I don't fundamentally have an issue with it other than it means that the conventions you decide on become really ******* important. Like you do not make those decisions lightly. ... you have to be comfortable breaking them. ... Now it's very early on in where we are with file based apps ... So now's the time to try that type of stuff because it gets harder to try it the more people to change things, the more people you have."*
- **Team agreement**: Simple, clear conventions are better than excessive settings/checkboxes
- Conventions help both tooling AND developers understand codebases
- **Recommendation**: Two-stage heuristic is simple enough to explain and defend

**Education & Discoverability** (Claudia Regio)
- Concerns about feature discoverability
- Need for explicit documentation and education updates
- Must call out conventions clearly if adopting this approach

**Implementation Location** (Rikki Gibson, Jason Malinowski, David Barbet)
- Current implementation: VS Code extension using Roslyn Language Server (not CPS)
- **Challenge**: Deciding where this logic should live has been "surprisingly challenging"
- May need to move/duplicate for VS for Windows support in the future
- **Approach**: Implement where convenient now, be prepared to move as needs evolve
- Documentation is more important than implementation location initially

#### Action Items

1. **Documentation Updates**:
   - Update heuristic detection steps ✓
   - Document current feature flags and their purpose ✓
   - Add recommended conventions (separate FBAs from project folders) ✓

2. **Future Work**:
   - Prepare for future changes (multi-file support, context picker)
   - Consider globbing patterns for large repos (performance optimization)
   - Investigate status bar integration to show current file mode --- or other visual queues/approaches

3. **Related PRs**:
   - [PR 687403](https://devdiv.visualstudio.com/DevDiv/_git/vs-green/pullrequest/687403): Add file-not-in-solution notification feature
   - [PR 692126](https://devdiv.visualstudio.com/DevDiv/_git/vs-green/pullrequest/692126): Add hard problems meeting process and FBA detection document

#### Notable Quotes

> "Embrace the platform conventions and the tooling will work better" - Team consensus

> "Are we OK basically telling people like if you want the best editor experience, here are the conventions you need to follow in your repository." - Jared Parsons

> "The conventions you decide on become really ******* important. Like you do not make those decisions lightly." - Damian Edwards

> "If you show the status on the status bar, maybe it is natural to include a such command (in-place) to allow the user to change between misc/file based app status?" - Lifeng Lu

> "and maybe persisted in workspace setting" - Jake Radzikowski

> "Phoenix rules: 3 space indent. That way everyone is equally unhappy about the outcome" - Jared Parsons (on tabs vs spaces)

> "Next time's hard problem: tabs vs. spaces." - Jason Malinowski (post-meeting humor)

## Follow-up

