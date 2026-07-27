#!/usr/bin/env python3
import os
import sys
import json
import subprocess
from datetime import datetime
import re

# ANSI terminal colors
class Colors:
    HEADER = '\033[95m'
    BLUE = '\033[94m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    RED = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    GRAY = '\033[90m'

    @classmethod
    def disable(cls):
        cls.HEADER = ''
        cls.BLUE = ''
        cls.GREEN = ''
        cls.YELLOW = ''
        cls.RED = ''
        cls.ENDC = ''
        cls.BOLD = ''
        cls.GRAY = ''

# Enable terminal colors on Windows if possible, otherwise disable
if os.name == 'nt':
    try:
        import ctypes
        kernel32 = ctypes.windll.kernel32
        kernel32.SetConsoleMode(kernel32.GetStdHandle(-11), 7)
    except Exception:
        Colors.disable()

def find_workspace_root():
    current = os.path.abspath(os.getcwd())
    while current:
        if os.path.exists(os.path.join(current, "tools")) and os.path.exists(os.path.join(current, "../gitizer")):
            return current
        if os.path.basename(current) == "gitic":
            return current
        parent = os.path.dirname(current)
        if parent == current:
            break
        current = parent
    return os.path.abspath(os.getcwd())

def to_pascal_case(s):
    words = re.split(r'[-_\s]+', s)
    return "".join(w.capitalize() for w in words if w)

def map_ts_path_to_csharp(relative_path):
    parts = relative_path.replace("\\", "/").split('/')
    mapped_parts = []
    for i, part in enumerate(parts):
        if not part:
            continue
        if i == len(parts) - 1:  # File name
            name_without_ext, ext = os.path.splitext(part)
            if ext.lower() in ('.ts', '.tsx'):
                if name_without_ext.lower() in ('main', 'index'):
                    mapped_parts.append("Program.cs")
                else:
                    mapped_parts.append(to_pascal_case(name_without_ext) + ".cs")
            else:
                mapped_parts.append(part)
        else:  # Directory name
            if i == 0 and part.lower() in ('src', 'tests'):
                mapped_parts.append(part.lower())
            else:
                mapped_parts.append(to_pascal_case(part))
    return "/".join(mapped_parts)

def get_namespace(target_relative_path):
    parts = target_relative_path.replace("\\", "/").split('/')
    namespace_parts = ["Gitic"]
    start_idx = 1 if parts and parts[0].lower() == 'src' else 0
    for part in parts[start_idx:-1]:
        if part:
            namespace_parts.append(part)
    return ".".join(namespace_parts)

def find_all_ts_files(gitizer_root):
    files = []
    for folder in ('src', 'tests'):
        folder_path = os.path.join(gitizer_root, folder)
        if os.path.exists(folder_path):
            for root, _, filenames in os.walk(folder_path):
                for filename in filenames:
                    if filename.endswith('.ts') or filename.endswith('.tsx'):
                        full_path = os.path.join(root, filename)
                        files.append(os.path.relpath(full_path, gitizer_root))
    return sorted(files)

def load_or_create_state(state_file_path, gitizer_root):
    if os.path.exists(state_file_path):
        try:
            with open(state_file_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            print(f"{Colors.YELLOW}Warning: Failed to load porting state: {e}. Re-creating state...{Colors.ENDC}")

    state = {
        "SourceDirectory": "../gitizer",
        "TargetDirectory": ".",
        "Files": []
    }

    ts_files = find_all_ts_files(gitizer_root)
    for ref_path in ts_files:
        target_ref_path = map_ts_path_to_csharp(ref_path)
        state["Files"].append({
            "SourceRelativePath": ref_path,
            "TargetRelativePath": target_ref_path,
            "Status": "Pending",
            "PortedAt": None
        })

    save_state(state_file_path, state)
    return state

def save_state(state_file_path, state):
    try:
        with open(state_file_path, 'w', encoding='utf-8') as f:
            json.dump(state, f, indent=2)
    except Exception as e:
        print(f"{Colors.RED}Error saving porting state: {e}{Colors.ENDC}")

def show_header():
    print(f"{Colors.HEADER}================================================================================{Colors.ENDC}")
    print(f"{Colors.BOLD}                   Gitizer to Gitic .NET Porting Coordinator                    {Colors.ENDC}")
    print(f"{Colors.HEADER}================================================================================{Colors.ENDC}")

def show_progress(state):
    total = len(state["Files"])
    ported = sum(1 for f in state["Files"] if f["Status"] == "Ported")
    skipped = sum(1 for f in state["Files"] if f["Status"] == "Skipped")
    pending = sum(1 for f in state["Files"] if f["Status"] == "Pending")
    pct = (ported / total * 100) if total > 0 else 0.0

    print("Progress: ", end="")
    print(f"{Colors.GREEN}{ported}/{total} Ported ({pct:.2f}%){Colors.ENDC} | ", end="")
    print(f"{Colors.YELLOW}{pending} Pending{Colors.ENDC} | ", end="")
    print(f"{Colors.GRAY}{skipped} Skipped{Colors.ENDC}")

    # Progress bar
    bar_width = 40
    filled_width = int(bar_width * (ported / total)) if total > 0 else 0
    print("[", end="")
    print(f"{Colors.GREEN}{'=' * filled_width}{Colors.ENDC}", end="")
    print(f"{' ' * (bar_width - filled_width)}]")
    print(f"{Colors.GRAY}--------------------------------------------------------------------------------{Colors.ENDC}")

def show_next_file(next_file):
    if next_file:
        print("Next recommended file to port:")
        print(f"  {Colors.YELLOW}Source: {next_file['SourceRelativePath']}{Colors.ENDC}")
        print(f"  {Colors.BLUE}Target: {next_file['TargetRelativePath']}{Colors.ENDC}")
    else:
        print(f"{Colors.GREEN}Great job! All files have been ported.{Colors.ENDC}")
    print(f"{Colors.GRAY}--------------------------------------------------------------------{Colors.ENDC}")

def port_file_interactive(file_entry, workspace_root, gitizer_root, state_file_path, state):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print(f"{Colors.YELLOW}Porting (Interactive Mode): {file_entry['SourceRelativePath']} -> {file_entry['TargetRelativePath']}{Colors.ENDC}\n")

    source_full_path = os.path.abspath(os.path.join(gitizer_root, file_entry['SourceRelativePath']))
    target_full_path = os.path.abspath(os.path.join(workspace_root, file_entry['TargetRelativePath']))

    print(f"Source Full Path: {source_full_path}")
    print(f"Target Full Path: {target_full_path}\n")

    confirm = input("Press ENTER to launch interactive Gemini session (or type 'cancel' to abort): ")
    if confirm.strip().lower() == 'cancel':
        return

    # Auto-create the target directory so Gemini can write into it directly
    os.makedirs(os.path.dirname(target_full_path), exist_ok=True)

    ns = get_namespace(file_entry['TargetRelativePath'])

    prompt = (
        f"You are a C# and TypeScript expert porting code from the Gitizer framework (TypeScript) to the Gitic framework (.NET C#).\n"
        f"We are porting:\n"
        f"- Source file (TypeScript): '{file_entry['SourceRelativePath']}'\n"
        f"- Target file (C#): '{file_entry['TargetRelativePath']}'\n\n"
        f"The content of the TypeScript source file is:\n"
        f"@{source_full_path}\n\n"
        f"Please:\n"
        f"1. Analyze the TypeScript source code and port all its functionality, types, and logic to idiomatic C# .NET.\n"
        f"2. Write the complete, compiled C# code to the target path: '{target_full_path}' (creating folders if they do not exist).\n"
        f"3. Keep the file structure, classes, and namespaces aligned with the original module structure: Namespace '{ns}'.\n"
        f"4. Assist me with compiling, linting, or testing this file as needed before completing.\n\n"
        f"Let's start! What is your initial draft of the ported C# code?"
    )

    try:
        print("\n--- Entering Gemini CLI Interactive Mode ---")
        # Run gemini process, inheriting the parent terminal's stdin/stdout
        subprocess.run(
            ["gemini", "-i", prompt, "--include-directories", "../gitizer"],
            cwd=workspace_root
        )
        print("--- Exited Gemini CLI Interactive Mode ---\n")

        valid_response = False
        while not valid_response:
            ans = input("Did you successfully port this file? (y = Ported, n = Keep Pending, s = Skip): ").strip().lower()
            if ans in ('y', 'yes'):
                file_entry['Status'] = 'Ported'
                file_entry['PortedAt'] = datetime.now().isoformat()
                valid_response = True
            elif ans in ('n', 'no'):
                file_entry['Status'] = 'Pending'
                valid_response = True
            elif ans in ('s', 'skip'):
                file_entry['Status'] = 'Skipped'
                valid_response = True

        save_state(state_file_path, state)
    except Exception as e:
        print(f"{Colors.RED}Error running Gemini: {e}{Colors.ENDC}")
        input("Press any key to return...")

def port_file_autonomous(file_entry, workspace_root, gitizer_root, state_file_path, state):
    source_full_path = os.path.abspath(os.path.join(gitizer_root, file_entry['SourceRelativePath']))
    target_full_path = os.path.abspath(os.path.join(workspace_root, file_entry['TargetRelativePath']))

    # Auto-create target directory
    os.makedirs(os.path.dirname(target_full_path), exist_ok=True)

    ns = get_namespace(file_entry['TargetRelativePath'])

    prompt = (
        f"You are a C# and TypeScript expert porting code from the Gitizer framework (TypeScript) to the Gitic framework (.NET C#).\n"
        f"We are porting:\n"
        f"- Source file (TypeScript): '{file_entry['SourceRelativePath']}'\n"
        f"- Target file (C#): '{file_entry['TargetRelativePath']}'\n\n"
        f"The content of the TypeScript source file is:\n"
        f"@{source_full_path}\n\n"
        f"Please port this file autonomously by performing the following actions:\n"
        f"1. Analyze the TypeScript source code and translate all its functionality, types, and logic to idiomatic C# .NET.\n"
        f"2. Write the complete, compiled C# code directly to the target path: '{target_full_path}' using your write_file or replace tool.\n"
        f"3. Ensure that you write the complete code without placeholders, and use the namespace '{ns}'.\n"
        f"4. If there are any missing dependencies or directories, create them as needed.\n"
        f"5. Once written, compile the project using 'dotnet build' to verify there are no compilation errors.\n"
        f"6. If there are any compilation errors, fix them until the build succeeds.\n\n"
        f"Proceed with porting now."
    )

    try:
        print(f"Launching Gemini in Autonomous YOLO mode for: {file_entry['SourceRelativePath']}...")
        # Run gemini process in YOLO/headless mode, inheriting stdin/stdout so we see progress
        res = subprocess.run(
            ["gemini", "-y", "--include-directories", "../gitizer", "-p", prompt],
            cwd=workspace_root
        )
        
        if res.returncode == 0:
            print(f"\n{Colors.GREEN}Successfully ported {file_entry['SourceRelativePath']}!{Colors.ENDC}")
            file_entry['Status'] = 'Ported'
            file_entry['PortedAt'] = datetime.now().isoformat()
            save_state(state_file_path, state)
            return True
        else:
            print(f"\n{Colors.RED}Gemini exited with non-zero code: {res.returncode}{Colors.ENDC}")
            return False
    except Exception as e:
        print(f"{Colors.RED}Error running Gemini autonomously: {e}{Colors.ENDC}")
        return False

def port_files_autonomous_batch(file_entries, workspace_root, gitizer_root, state_file_path, state):
    prompt_files_part = ""
    for i, file_entry in enumerate(file_entries):
        source_full_path = os.path.abspath(os.path.join(gitizer_root, file_entry['SourceRelativePath']))
        target_full_path = os.path.abspath(os.path.join(workspace_root, file_entry['TargetRelativePath']))
        os.makedirs(os.path.dirname(target_full_path), exist_ok=True)
        ns = get_namespace(file_entry['TargetRelativePath'])
        prompt_files_part += (
            f"File {i+1}:\n"
            f"  - Source file (TypeScript): '{file_entry['SourceRelativePath']}'\n"
            f"  - Target file (C#): '{file_entry['TargetRelativePath']}'\n"
            f"  - Target Namespace: '{ns}'\n"
            f"  - Target Full Path: '{target_full_path}'\n"
            f"  - Source Content reference: @{source_full_path}\n\n"
        )

    prompt = (
        f"You are a C# and TypeScript expert porting code from the Gitizer framework (TypeScript) to the Gitic framework (.NET C#).\n"
        f"We are porting a batch of {len(file_entries)} files in this single session:\n\n"
        f"{prompt_files_part}"
        f"Please port ALL of these {len(file_entries)} files autonomously by performing the following actions for EACH file:\n"
        f"1. Analyze the TypeScript source code and translate all its functionality, types, and logic to idiomatic C# .NET.\n"
        f"2. Write the complete, compiled C# code directly to the target path using your write_file or replace tool.\n"
        f"3. Ensure that you write the complete code without placeholders, and use the namespace specified for that file.\n"
        f"4. If there are any missing dependencies or directories, create them as needed.\n"
        f"5. Once written, compile the project using 'dotnet build' to verify there are no compilation errors.\n"
        f"6. If there are any compilation errors, fix them until the build succeeds.\n\n"
        f"Proceed with porting all {len(file_entries)} files in this batch now."
    )

    try:
        print(f"Launching Gemini in Autonomous YOLO mode for a batch of {len(file_entries)} files:")
        for f in file_entries:
            print(f"  - {f['SourceRelativePath']}")
        print()
        
        # Run gemini process in YOLO/headless mode, inheriting stdin/stdout so we see progress
        res = subprocess.run(
            ["gemini", "-y", "--include-directories", "../gitizer", "-p", prompt],
            cwd=workspace_root
        )
        
        if res.returncode == 0:
            print(f"\n{Colors.GREEN}Successfully completed autonomous porting session for the batch!{Colors.ENDC}")
            for file_entry in file_entries:
                file_entry['Status'] = 'Ported'
                file_entry['PortedAt'] = datetime.now().isoformat()
            save_state(state_file_path, state)
            return True
        else:
            print(f"\n{Colors.RED}Gemini exited with non-zero code: {res.returncode}{Colors.ENDC}")
            return False
    except Exception as e:
        print(f"{Colors.RED}Error running Gemini autonomously in batch mode: {e}{Colors.ENDC}")
        return False

def run_fully_autonomous_crawl(state, workspace_root, gitizer_root, state_file_path):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print(f"{Colors.GREEN}{Colors.BOLD}Starting Fully Autonomous Porting Crawl (Unattended){Colors.ENDC}\n")
    
    pending_files = [f for f in state["Files"] if f["Status"] == "Pending"]
    if not pending_files:
        print("No pending files to port!")
        input("Press ENTER to return...")
        return

    print(f"Found {len(pending_files)} pending files. They will be ported in batches of 5 files per session.")
    print("Each batch will be automatically mapped, ported, compiled, and verified together in the same agentic session.")
    print("If a batch fails to port or compile, the crawl will halt to let you decide how to proceed.\n")
    
    confirm = input("Press ENTER to start the crawl (or type 'cancel' to abort): ")
    if confirm.strip().lower() == 'cancel':
        return

    batch_size = 5
    # Calculate how many batches we have
    batches = [pending_files[i:i + batch_size] for i in range(0, len(pending_files), batch_size)]

    for i, batch in enumerate(batches):
        print(f"\n{Colors.HEADER}================================================================================{Colors.ENDC}")
        print(f"{Colors.BOLD}[Batch {i+1}/{len(batches)}] Porting {len(batch)} files autonomously{Colors.ENDC}")
        for f in batch:
            print(f"  - {f['SourceRelativePath']}")
        print(f"{Colors.HEADER}================================================================================{Colors.ENDC}\n")
        
        success = port_files_autonomous_batch(batch, workspace_root, gitizer_root, state_file_path, state)
        if not success:
            print(f"\n{Colors.RED}Autonomous porting batch failed or was cancelled.{Colors.ENDC}")
            ans = input("Do you want to continue the crawl for the remaining batches? (y/n): ").strip().lower()
            if ans not in ('y', 'yes'):
                print("Autonomous crawl terminated by user.")
                input("Press ENTER to return to menu...")
                break
        else:
            # Short pause between batches to let the user read the success message
            print(f"{Colors.GRAY}Advancing to the next batch of files...{Colors.ENDC}")

def select_and_port_file(state, workspace_root, gitizer_root, state_file_path):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print("Select a file to port:\n")

    pending_files = [f for f in state["Files"] if f["Status"] != "Ported"]
    if not pending_files:
        print(f"{Colors.GREEN}No pending files available! All files ported.{Colors.ENDC}")
        input("Press any key to return...")
        return

    for i, f in enumerate(pending_files):
        color = Colors.GRAY if f["Status"] == "Skipped" else Colors.YELLOW
        print(f"  [{i+1}] {color}{f['SourceRelativePath']} -> {f['TargetRelativePath']} ({f['Status']}){Colors.ENDC}")

    print("  [0] Back to Menu\n")
    try:
        choice = int(input(f"Enter choice (0-{len(pending_files)}): ").strip())
        if 0 < choice <= len(pending_files):
            selected_file = pending_files[choice - 1]
            print("\nPorting Modes:")
            print("  [1] Autonomous YOLO Mode (Let Gemini port, write, and build automatically)")
            print("  [2] Interactive Mode (Enter live discussion session with Gemini)")
            mode = input("Select mode (1 or 2): ").strip()
            if mode == '1':
                port_file_autonomous(selected_file, workspace_root, gitizer_root, state_file_path, state)
                input("\nPorting process completed. Press ENTER to continue...")
            elif mode == '2':
                port_file_interactive(selected_file, workspace_root, gitizer_root, state_file_path, state)
    except ValueError:
        pass

def show_detailed_report(state):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print("Detailed Progress Report:\n")

    statuses = ["Ported", "Pending", "Skipped"]
    for status in statuses:
        group = [f for f in state["Files"] if f["Status"] == status]
        color = Colors.GREEN if status == "Ported" else (Colors.YELLOW if status == "Pending" else Colors.GRAY)
        print(f"{color}--- {status} ({len(group)} files) ---{Colors.ENDC}")
        for f in group:
            if status == "Ported" and f.get("PortedAt"):
                print(f"  {f['SourceRelativePath']} -> {f['TargetRelativePath']} (Ported on {f['PortedAt'][:16]})")
            else:
                print(f"  {f['SourceRelativePath']} -> {f['TargetRelativePath']}")
        print()

    input("Press any key to return to Menu...")

def mark_file_status_manually(state, state_file_path):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print("Mark File Status Manually:\n")

    for i, f in enumerate(state["Files"]):
        color = Colors.GREEN if f["Status"] == "Ported" else (Colors.YELLOW if f["Status"] == "Pending" else Colors.GRAY)
        print(f"  [{i+1}] {color}{f['SourceRelativePath']} ({f['Status']}){Colors.ENDC}")

    print("  [0] Back to Menu\n")
    try:
        choice = int(input(f"Select file (0-{len(state['Files'])}): ").strip())
        if 0 < choice <= len(state["Files"]):
            file_entry = state["Files"][choice - 1]
            print(f"\nSelected file: {file_entry['SourceRelativePath']}")
            ans = input("Enter new status (p = Pending, s = Skipped, y = Ported): ").strip().lower()
            if ans in ('y', 'ported'):
                file_entry['Status'] = 'Ported'
                file_entry['PortedAt'] = datetime.now().isoformat()
            elif ans in ('p', 'pending'):
                file_entry['Status'] = 'Pending'
                file_entry['PortedAt'] = None
            elif ans in ('s', 'skipped'):
                file_entry['Status'] = 'Skipped'
                file_entry['PortedAt'] = None
            save_state(state_file_path, state)
            print("Status updated successfully!")
            input("Press any key to continue...")
    except ValueError:
        pass

def scan_for_new_files(state, gitizer_root, state_file_path):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print("Scanning for new source files...\n")

    all_source_files = find_all_ts_files(gitizer_root)
    added_count = 0

    tracked_paths = {f["SourceRelativePath"] for f in state["Files"]}

    for src_file in all_source_files:
        if src_file not in tracked_paths:
            target_relative = map_ts_path_to_csharp(src_file)
            state["Files"].append({
                "SourceRelativePath": src_file,
                "TargetRelativePath": target_relative,
                "Status": "Pending",
                "PortedAt": None
            })
            print(f"  {Colors.GREEN}Tracked new file: {src_file} -> {target_relative}{Colors.ENDC}")
            added_count += 1

    if added_count > 0:
        save_state(state_file_path, state)
        print(f"\nSuccessfully added {added_count} new files to tracking!")
    else:
        print("No new files found. Everything is up to date.")

    input("\nPress any key to return...")

def reset_all_progress(state, state_file_path):
    os.system('cls' if os.name == 'nt' else 'clear')
    show_header()
    print(f"{Colors.RED}WARNING: You are about to reset all progress tracking!{Colors.ENDC}")
    confirm = input("Are you absolutely sure? Type 'YES' to confirm: ")
    if confirm == 'YES':
        for f in state["Files"]:
            f["Status"] = "Pending"
            f["PortedAt"] = None
        save_state(state_file_path, state)
        print("\nAll progress has been reset.")
    else:
        print("\nReset aborted.")
    input("Press any key to continue...")

def main():
    workspace_root = find_workspace_root()
    gitizer_root = os.path.abspath(os.path.join(workspace_root, "../gitizer"))

    if not os.path.exists(gitizer_root):
        print(f"{Colors.RED}Error: Gitizer root directory not found at: {gitizer_root}{Colors.ENDC}")
        return

    state_file_path = os.path.join(workspace_root, "porting_state.json")
    state = load_or_create_state(state_file_path, gitizer_root)

    running = True
    while running:
        os.system('cls' if os.name == 'nt' else 'clear')
        show_header()
        show_progress(state)

        next_file = next((f for f in state["Files"] if f["Status"] == "Pending"), None)
        show_next_file(next_file)

        print("Menu Options:")
        if next_file:
            print(f"  [1] Port Next Recommended File (Autonomous YOLO Mode)")
            print(f"  [2] Port Next Recommended File (Interactive Mode)")
        else:
            print(f"  [1] {Colors.GREEN}Port Next Recommended File (All files ported!){Colors.ENDC}")
            print("  [2] Port Next Recommended File (All files ported!)")
        
        print("  [3] Run Fully Autonomous Crawl (Unattended Loop)")
        print("  [4] Select Specific File to Port")
        print("  [5] View Detailed Progress Report")
        print("  [6] Mark File Status Manually")
        print("  [7] Scan for New Files")
        print("  [8] Reset All Progress")
        print("  [9] Quit")
        print(f"{Colors.HEADER}================================================================================{Colors.ENDC}")
        
        choice = input("Choose an option (1-9): ").strip()
        if choice == '1':
            if next_file:
                port_file_autonomous(next_file, workspace_root, gitizer_root, state_file_path, state)
                input("\nPorting process completed. Press ENTER to continue...")
            else:
                input("All files are already ported! Press ENTER to continue...")
        elif choice == '2':
            if next_file:
                port_file_interactive(next_file, workspace_root, gitizer_root, state_file_path, state)
            else:
                input("All files are already ported! Press ENTER to continue...")
        elif choice == '3':
            run_fully_autonomous_crawl(state, workspace_root, gitizer_root, state_file_path)
        elif choice == '4':
            select_and_port_file(state, workspace_root, gitizer_root, state_file_path)
        elif choice == '5':
            show_detailed_report(state)
        elif choice == '6':
            mark_file_status_manually(state, state_file_path)
        elif choice == '7':
            scan_for_new_files(state, gitizer_root, state_file_path)
        elif choice == '8':
            reset_all_progress(state, state_file_path)
        elif choice == '9':
            running = False
        else:
            print(f"{Colors.YELLOW}Invalid option. Press ENTER to retry...{Colors.ENDC}")
            input()

if __name__ == '__main__':
    main()