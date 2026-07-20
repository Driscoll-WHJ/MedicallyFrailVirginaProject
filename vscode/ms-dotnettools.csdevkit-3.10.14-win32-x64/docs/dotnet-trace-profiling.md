# Dotnet-Trace Profiling for Server Process

## Overview

This document describes the design and implementation approach for collecting runtime traces and profiling data from the C# Dev Kit server process (`serverProcess`) using `dotnet-trace`. The implementation is inspired by the C# extension's approach for their LSP server.

## Background

The C# Dev Kit extension launches a .NET server process (`Microsoft.VisualStudio.Code.Server`) that handles brokered services, solution management, project system hosting, and other server-side operations. To diagnose performance issues, memory leaks, or understand runtime behavior, we need the ability to collect detailed profiling and tracing information from this process.

Currently, the extension supports basic EventPipe profiling through environment variables (see `src/profiling.ts`), but this approach has limitations:
- Requires process restart to enable/disable
- Requires manual setup of environment variables
- Limited control over trace duration and output location
- No interactive UI for users

## Goals

1. **Interactive Trace Collection**: Provide a VS Code command that users can run at any time to collect traces from running C# Dev Kit processes
2. **Multi-Process Support**: Allow users to select and trace multiple processes simultaneously (server and project system host)
3. **User-Friendly Experience**: Guide users through the process with dialogs, progress notifications, and clear feedback
4. **Flexible Configuration**: Allow users to customize trace providers, event levels, and output locations
5. **Automatic Tool Management**: Handle `dotnet-trace` installation if not present
6. **Rich Trace Data**: Collect comprehensive profiling data including:
   - CPU sampling and JIT events
   - CLR runtime events
   - ServiceHub-specific events
   - Project system and CPS events
   - Custom extension event sources

## Design

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│   VS Code Extension (Client)                                    │
│                                                                 │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │ Command: recordServerTrace                             │   │
│  └────────────────────────────────────────────────────────────┘   │
│              │                                                 │
│              ▼                                                 │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │ Trace Controller                                       │   │
│  │ - Track all C#DK process PIDs                         │   │
│  │ - Show multi-select for process selection             │   │
│  │ - Verify/install dotnet-trace                         │   │
│  │ - Prompt for output location                          │   │
│  │ - Configure providers                                 │   │
│  │ - Start/stop multiple trace collections (parallel)    │   │
│  └────────────────────────────────────────────────────────────┘   │
│              │                                                 │
└──────────────┼─────────────────────────────────────────────────┘
               │
               ▼ (uses process IDs - one dotnet-trace per process)
┌─────────────────────────────────────────────────────────────────┐
│   C# Dev Kit Processes                                          │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐     │
│  │ Microsoft.VisualStudio.Code.Server                   │     │
│  │ - Main server, ServiceHub controller                 │     │
│  └──────────────────────────────────────────────────────────┘     │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐     │
│  │ Project System Host                                  │     │
│  │ - CPS, MSBuild, design-time builds                  │     │
│  └──────────────────────────────────────────────────────────┘     │
│                                                                 │
│   Each process emits EventPipe events                          │
│   Each monitored by separate dotnet-trace instance             │
└─────────────────────────────────────────────────────────────────┘
               │
               ▼ (writes separate trace files)
┌─────────────────────────────────────────────────────────────────┐
│   Trace Output (.nettrace files per process)                    │
│   - CSharp_DevKit_server_<pid>_<timestamp>.nettrace             │
│   - CSharp_DevKit_pshost_<pid>_<timestamp>.nettrace             │
│   - Can be analyzed in VS/PerfView                              │
└─────────────────────────────────────────────────────────────────┘
```

### Component Design

#### 1. Multi-Process Tracking

C# Dev Kit runs multiple .NET processes that need to be tracked:

```typescript
interface CsDevKitProcessInfo {
    name: string;              // Display name for user selection
    pid: number | undefined;   // Process ID
    processType: 'server' | 'pshost';
    description: string;       // Description for tooltip
}

class ProcessTracker {
    private processes: Map<string, CsDevKitProcessInfo> = new Map();
    
    // Called when server process starts
    trackServerProcess(pid: number) {
        this.processes.set('server', {
            name: 'Microsoft.VisualStudio.Code.Server',
            pid,
            processType: 'server',
            description: 'Main server process (ServiceHub, brokered services)'
        });
    }
    
    // Called when project system host starts
    trackProjectSystemHost(pid: number) {
        this.processes.set('pshost', {
            name: 'Project System Host',
            pid,
            processType: 'pshost',
            description: 'CPS, MSBuild, design-time builds'
        });
    }
    
    getRunningProcesses(): CsDevKitProcessInfo[] {
        return Array.from(this.processes.values())
            .filter(p => p.pid !== undefined);
    }
}
```

**Implementation considerations:**
- Track all C# Dev Kit process PIDs in a centralized tracker
- Update PIDs when processes start/restart
- Handle graceful degradation if some processes aren't running
- Provide clear process identification in UI

#### 2. Trace Command Registration

Register a new command `csdevkit.recordServerTrace` that can be invoked from the command palette:

```typescript
// In src/extension.ts or new src/tracing/tracing.ts
export function registerTraceCommand(
    context: vscode.ExtensionContext,
    processTracker: ProcessTracker,
    outputChannel: vscode.LogOutputChannel
): void {
    context.subscriptions.push(
        vscode.commands.registerCommand('csdevkit.recordServerTrace', async () => {
            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: 'dotnet-trace',
                    cancellable: true,
                },
                async (progress, token) => {
                    await executeDotNetTraceCommand(
                        processTracker,
                        progress,
                        outputChannel,
                        token
                    );
                }
            );
        })
    );
}
```

#### 3. Trace Collection Workflow

The trace collection workflow follows these steps:

1. **Process Discovery and Selection**
   - Query ProcessTracker for all running C# Dev Kit processes
     - Show quick pick with multi-select for available processes:
         * Microsoft.VisualStudio.Code.Server (if running)
         * Project System Host (if running)
   - Allow user to select one or multiple processes to trace
   - Validate selected processes are still alive

2. **Tool Verification**
   - Check if `dotnet-trace` is installed globally
   - If not, prompt user to install it
   - Execute `dotnet tool install --global dotnet-trace` if user confirms

3. **User Configuration**
   - Prompt for output folder (default to first workspace folder)
   - Show input box with pre-populated trace arguments (applied to all selected processes)
   - Allow user to customize providers and event levels

4. **Trace Collection (Parallel)**
   - Create or reuse terminals for each selected process:
    * "dotnet-trace (Server)"
    * "dotnet-trace (PS Host)"
    - Execute separate `dotnet-trace collect --process-id <pid>` commands in parallel
    - Show progress notification with count (e.g., "Recording traces from 2 processes...")
   - Allow user to stop all traces via cancellation or Ctrl+C in terminals

5. **Completion**
   - Wait for all trace sessions to complete
   - Notify user when all traces are complete
   - Provide information about output file locations (one per process)
   - Suggest tools for analysis (Visual Studio, PerfView)
   - Option to open folder containing trace files

#### 4. Default Trace Providers

Based on the C# extension's approach and C# Dev Kit's architecture, we should collect events from:

```typescript
const DEFAULT_PROVIDERS = [
    'Microsoft-DotNETCore-SampleProfiler',           // CPU sampling
    'Microsoft-Windows-DotNETRuntime',               // CLR runtime events
    'Microsoft-VisualStudio-CpsCore',               // CPS events
];

const DEFAULT_ARGS = 
    `--process-id ${processId} ` +
    `--clreventlevel informational ` +
    `--providers "${DEFAULT_PROVIDERS.join(',')}"`;
```

**Event levels:**
- Use `informational` or higher for most providers
- Can be customized by users for specific scenarios

#### 5. Terminal Integration

Use VS Code's terminal API with shell integration for better control:

```typescript
async function runDotnetTrace(
    args: string[],
    terminal: vscode.Terminal,
    token: vscode.CancellationToken
): Promise<void> {
    terminal.show();
    const command = `dotnet-trace ${args.join(' ')}`;
    const shellIntegration = terminal.shellIntegration;
    
    if (shellIntegration) {
        // With shell integration, we can monitor command completion
        const execution = shellIntegration.executeCommand(command);
        
        // Handle cancellation
        const cancelDisposable = token.onCancellationRequested(() => {
            terminal.sendText('\u0003'); // Send Ctrl+C
        });
        
        // Wait for command to complete
        await new Promise<void>((resolve) => {
            vscode.window.onDidEndTerminalShellExecution((e) => {
                if (e.execution === execution) {
                    cancelDisposable.dispose();
                    resolve();
                }
            });
        });
    } else {
        // Fallback without shell integration
        terminal.sendText(command);
    }
}
```

**Why use terminal instead of child_process:**
- `dotnet-trace` requires shell input to stop the trace (Ctrl+C)
- Using a pseudo-terminal makes it easier to send signals
- Users can interact with the terminal directly if needed
- VS Code's terminal API handles cross-platform differences

#### 6. File Handling

The trace output file has a specific naming pattern:

```
<output-folder>/CSharp_DevKit_<process-type>_<pid>_<timestamp>.nettrace
```

Where `<process-type>` is either `server` or `pshost`.

**Considerations:**
- Validate the output folder exists and is writable
- Don't overwrite existing trace files
- Provide user feedback about the output file location
- Consider offering to open the file location after collection

### User Experience Flow

1. **Starting a Trace**
   ```
   User: Opens command palette
   User: Types "Record C#DK Trace"
   Extension: Shows progress notification "Initializing dotnet-trace..."
   Extension: Checks if dotnet-trace is installed
   Extension: Discovers running C# Dev Kit processes
   ```

2. **Tool Installation (if needed)**
   ```
   Extension: Shows dialog "dotnet-trace not found, run 'dotnet tool install --global dotnet-trace' to install it?"
   User: Clicks "Install"
   Extension: Shows progress "Installing dotnet-trace..."
   Extension: Executes installation command
   Extension: Shows success/failure message
   ```

3. **Process Selection**
   ```
   Extension: Shows multi-select quick pick "Select C# Dev Kit processes to trace:"
   Options (with checkboxes):
     ☑ Microsoft.VisualStudio.Code.Server (PID: 12345)
       Main server process (ServiceHub, brokered services)
     ☑ Project System Host (PID: 12346)
       CPS, MSBuild, design-time builds
   User: Selects one or more processes (or cancels)
   ```

4. **Configuration**
   ```
   Extension: Shows folder picker dialog "Select Folder to Save Trace Files"
   User: Selects folder (or cancels)
   Extension: Shows input box with default args: "--clreventlevel informational --providers ..."
   Note: "These settings will apply to all selected processes"
   User: Accepts or modifies the arguments
   ```

5. **Collection**
   ```
   Extension: Creates/shows terminals for each selected process:
     - "dotnet-trace (Server)"
     - "dotnet-trace (PS Host)"
   Extension: Shows progress "Recording traces from 2 processes..." (cancellable)
   Extension: Executes in parallel:
     Terminal 1: dotnet-trace collect --process-id 12345 ...
     Terminal 2: dotnet-trace collect --process-id 12346 ...
   Terminals: Show real-time output from each dotnet-trace instance
   ```

6. **Stopping**
   ```
   User: Clicks "Cancel" button OR presses Ctrl+C in each terminal
   Extension: Sends Ctrl+C to all active trace terminals
   dotnet-trace: Finalizes trace files (one per process)
   Extension: Shows completion notification:
     "Trace collection complete. Generated 2 trace files:"
     - CSharp_DevKit_server_12345_20231205_143022.nettrace
     - CSharp_DevKit_pshost_12346_20231205_143022.nettrace
     [Open Folder] [OK]
   ```

### Error Handling

| Error Scenario | Handling Approach |
|---------------|-------------------|
| No C# Dev Kit processes running | Show error: "No C# Dev Kit processes found. Please open a solution or wait for extension to initialize." |
| Process terminated during trace | Catch error, notify user which process failed, continue other traces, save partial traces if available |
| dotnet-trace not installed | Prompt user to install it, offer automatic installation |
| Installation fails | Show error with output channel link, suggest manual installation |
| Invalid output folder | Show error, re-prompt for valid folder |
| One trace fails in multi-process | Show warning, continue other traces, provide partial results |
| User cancels at any step | Clean up all trace sessions, dispose terminals, show informational message |

## Implementation Plan

### Phase 1: Core Infrastructure (MVP)
1. Create `ProcessTracker` class to track all C# Dev Kit process PIDs
2. Instrument `serverHost.ts` to track server process PID
3. Instrument process spawning locations to track PS Host PIDs
4. Create `src/tracing/tracing.ts` with multi-process support
5. Implement process validation for multiple processes
6. Register the trace command in `extension.ts`
7. Implement basic tool verification

### Phase 2: Multi-Process User Experience
1. Implement process discovery and multi-select quick pick UI
2. Implement folder picker dialog
3. Implement argument configuration input box
4. Create multiple terminals (one per selected process)
5. Execute parallel dotnet-trace commands
6. Add coordinated progress notifications
7. Handle cancellation across multiple trace sessions
8. Implement custom file naming: `CSharp_DevKit_<process-type>_<pid>_<timestamp>.nettrace`

## References

### External Documentation
- [dotnet-trace documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [EventPipe documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventpipe)
- [VS Code Terminal API](https://code.visualstudio.com/api/references/vscode-api#Terminal)

### Related Code
- C# Extension profiling: [profiling.ts](https://github.com/dotnet/vscode-csharp/blob/5b7e0b701a05578d58fa8ed3db94f80cfe816733/src/lsptoolshost/profiling/profiling.ts)
- Current C# Dev Kit profiling: `src/profiling.ts`
- Server host implementation: `src/serverHost.ts`

## Success Metrics

- Users can successfully collect traces without external documentation
- Installation success rate for dotnet-trace
- Number of successful trace collections (single vs. multi-process)
- Average trace file size and duration per process type
- Process selection patterns (which processes are traced most often)
- Multi-process trace success rate
- User feedback on ease of use

## Future Plan

### Phase 3: Polish (Future)
1. Implement tool auto-installation flow
2. Add comprehensive error handling for multi-process scenarios
3. Add telemetry for feature usage (including multi-process metrics)
4. Add configuration settings for default process selection
5. Improve UI feedback for individual process status
6. Write user documentation with multi-process examples

### Phase 4: Advanced Features (Future)
1. Trace analysis integration
2. Custom provider configurations per process type
3. Automatic trace collection on crashes
4. Integration with VS Code testing views
5. Trace correlation across processes

### Additional Future Enhancements
1. **Preset Configurations**: Pre-defined provider sets per process type (e.g., PS Host gets MSBuild providers)
2. **Automated Analysis**: Basic analysis within VS Code with cross-process correlation
3. **Remote Trace Collection**: Support for collecting traces from remote dev containers
4. **Advanced Correlation**: Merge and correlate traces from multiple processes into unified view
5. **Crash Dumps**: Automatic trace collection when any server process crashes
6. **Smart Selection**: AI-suggested process selection based on user's current task
7. **Differential Tracing**: Compare traces across processes to identify bottlenecks
