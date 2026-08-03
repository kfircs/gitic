# Gitic Command Line Tab-Completion

Gitic is powered by `System.CommandLine`, which provides built-in support for command-line tab completion. This allows you to press `Tab` to automatically complete commands, option names, and option values in your shell.

## Setup Instructions

Depending on the shell you are using, follow the instructions below to enable tab-completion for the `gitic` tool.

### Bash

To enable completion in Bash, add the following script to your `~/.bashrc` file:

```bash
# Gitic completion for Bash
_gitic_complete() {
  local word=${COMP_WORDS[COMP_CWORD]}
  local completions
  completions=$(gitic [complete] "${COMP_WORDS[@]:1}")
  COMPREPLY=( $(compgen -W "$completions" -- "$word") )
}
complete -f -F _gitic_complete gitic
```

After updating `~/.bashrc`, reload your shell:
```bash
source ~/.bashrc
```

---

### Zsh

To enable completion in Zsh, add the following script to your `~/.zshrc` file:

```zsh
# Gitic completion for Zsh
_gitic_complete() {
  local completions
  completions=($(gitic [complete] "${words[@]:1}"))
  _describe 'gitic' completions
}
compdef _gitic_complete gitic
```

After updating `~/.zshrc`, reload your shell:
```zsh
source ~/.zshrc
```

---

### PowerShell

To enable completion in PowerShell, add the following script block to your PowerShell profile (`$PROFILE`):

```powershell
# Gitic completion for PowerShell
Register-ArgumentCompleter -Native -CommandName gitic -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $text = $commandAst.ToString()
    $args = $text.Split(' ') | Select-Object -Skip 1
    $completions = gitic [complete] $args
    foreach ($completion in $completions) {
        if ($completion -like "$wordToComplete*") {
            [System.Management.Automation.CompletionResult]::new($completion, $completion, 'ParameterValue', $completion)
        }
    }
}
```

After updating your profile, reload it:
```powershell
. $PROFILE
```

---

### Fish

To enable completion in Fish, create a completion script at `~/.config/fish/completions/gitic.fish`:

```fish
# Gitic completion for Fish
complete -f -c gitic -a '(gitic [complete] (commandline -opc)[2..-1])'
```
