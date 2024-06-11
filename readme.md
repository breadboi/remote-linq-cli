# Remote LINQPad Script Runner

This repository contains a command-line interface (CLI) application designed to facilitate the searching, downloading, and local execution of LINQPad scripts stored in GitHub repositories. The application leverages the GitHub API to fetch scripts based on user input and runs them locally using LINQPad.

## Features

- **GitHub Integration**: Search for LINQPad scripts within any GitHub repository.
- **Script Execution**: Run LINQPad scripts directly from the command line or through a Terminal User Interface (TUI).
- **Script Management**: Download and save scripts locally for offline access and execution.

## Getting Started

### Prerequisites

- .NET 5.0 or higher
- A GitHub Personal Access Token (PAT) with permissions to access repositories.
- LINQPad installed on your machine (the application assumes LINQPad 8).

### Configuration

1. Clone the repository to your local machine.
2. Navigate to the `RemoteLinqCli` directory.
3. Create an `appsettings.json` file with the following structure, replacing the placeholders with your actual GitHub settings:
```json
{ 
	"GitHubSettings": { 
		"Pat": "<Your_GitHub_Personal_Access_Token>",
		"Org": "<Default_Organization>",
		"Repo": "<Default_Repository>" 
	} 
}
```

### Usage

The application can be run in two modes:

1. **Command-Line Arguments Mode**: Provide the repository owner, repository name, and script path as command-line arguments.
2. **Interactive Terminal User Interface (TUI) Mode**: Run the application without arguments to launch the TUI, allowing for interactive searching and execution of scripts.