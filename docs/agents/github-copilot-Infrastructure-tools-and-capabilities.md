# Available Tools

This document lists all available tools, grouped by category, with brief descriptions of their functions, parameters, and outputs.

## File and Directory Operations

### create_directory
- **Function**: Create a new directory structure in the workspace, recursively creating all directories in the path.
- **Parameters**: dirPath (string, absolute path to the directory).
- **Outputs**: Success confirmation or error if creation fails.

### create_file
- **Function**: Create a new file in the workspace with specified content. The directory is created if it does not exist.
- **Parameters**: content (string, content to write), filePath (string, absolute path to the file).
- **Outputs**: Success confirmation or error if creation fails.

### list_dir
- **Function**: List the contents of a directory, showing child names (folders end with /).
- **Parameters**: path (string, absolute path to the directory).
- **Outputs**: List of child items or error if directory not found.

### read_file
- **Function**: Read the contents of a file, specifying line ranges (1-based).
- **Parameters**: endLine (number, inclusive end line), filePath (string, absolute path), startLine (number, start line).
- **Outputs**: File content within the range or error if file not found/readable.

### replace_string_in_file
- **Function**: Edit an existing file by replacing a specific string with new content. Requires exact matching with surrounding context.
- **Parameters**: filePath (string, absolute path), newString (string, replacement text), oldString (string, exact text to replace).
- **Outputs**: Success confirmation or error if replacement fails.

### file_search
- **Function**: Search for files in the workspace by glob pattern, returning paths of matching files.
- **Parameters**: maxResults (number, optional max results), query (string, glob pattern or absolute path).
- **Outputs**: List of matching file paths or error if search fails.

### grep_search
- **Function**: Perform a text search in the workspace using exact strings or regex, with options for case-insensitivity and file patterns.
- **Parameters**: includeIgnoredFiles (boolean, optional), includePattern (string, optional glob pattern), isRegexp (boolean), maxResults (number, optional), query (string, search pattern).
- **Outputs**: List of matches with file, line, and content snippets or error if search fails.

### semantic_search
- **Function**: Run a natural language search for relevant code or documentation in the workspace.
- **Parameters**: query (string, natural language query).
- **Outputs**: Relevant code snippets or full workspace contents if small, or error if no matches.

### get_changed_files
- **Function**: Get git diffs of current file changes in a git repository.
- **Parameters**: repositoryPath (string, optional absolute path to repo), sourceControlState (array of strings, optional: 'staged', 'unstaged', 'merge-conflicts').
- **Outputs**: List of changed files with diffs or error if not a git repo.

### view_image
- **Function**: View the contents of an image file (png, jpg, jpeg, gif, webp).
- **Parameters**: filePath (string, absolute path to image).
- **Outputs**: Image content or error if file not supported/readable.

## Notebook Operations

### create_new_jupyter_notebook
- **Function**: Generate a new Jupyter Notebook (.ipynb) in VS Code.
- **Parameters**: query (string, description of the notebook to create).
- **Outputs**: Success confirmation with file path or error if creation fails.

### edit_notebook_file
- **Function**: Edit an existing Notebook file by inserting, deleting, or editing cells.
- **Parameters**: cellId (string, cell ID or 'TOP'/'BOTTOM'), editType (string: 'insert', 'delete', 'edit'), filePath (string, absolute path), language (string, cell language), newCode (string or array, new cell content).
- **Outputs**: Success confirmation or error if edit fails.

### run_notebook_cell
- **Function**: Run a code cell in a notebook and return execution output.
- **Parameters**: cellId (string, code cell ID), continueOnError (boolean, optional), filePath (string, absolute path), reason (string, optional explanation).
- **Outputs**: Cell execution output, including results and errors.

### copilot_getNotebookSummary
- **Function**: Get a summary of a notebook, including cell details, execution info, and output mime types.
- **Parameters**: filePath (string, absolute path to notebook).
- **Outputs**: Structured summary of cells or error if file not found.

## Terminal and Execution

### run_in_terminal
- **Function**: Execute commands in a persistent terminal session on the user's machine.
- **Parameters**: command (string), explanation (string, one-sentence description), goal (string, short purpose), isBackground (boolean), timeout (number, optional milliseconds).
- **Outputs**: Command output, exit code, or timeout status.

### await_terminal
- **Function**: Wait for a background terminal command to complete.
- **Parameters**: id (string, terminal ID from run_in_terminal), timeout (number, optional milliseconds).
- **Outputs**: Output, exit code, or timeout status.

### get_terminal_output
- **Function**: Get the output of a previously started terminal command.
- **Parameters**: id (string, terminal ID).
- **Outputs**: Accumulated output or error if ID invalid.

### kill_terminal
- **Function**: Kill a background terminal by ID.
- **Parameters**: id (string, terminal ID).
- **Outputs**: Success confirmation or error if ID invalid.

### create_and_run_task
- **Function**: Create and run a build, run, or custom task in the workspace.
- **Parameters**: task (object with label, type, command, etc.), workspaceFolder (string, absolute path).
- **Outputs**: Task execution results or error if setup fails.

### terminal_last_command
- **Function**: Get the last command run in the active terminal.
- **Parameters**: None.
- **Outputs**: Last command string or error if no active terminal.

### terminal_selection
- **Function**: Get the current selection in the active terminal.
- **Parameters**: None.
- **Outputs**: Selected text or error if no selection.

## Web and External Content

### fetch_webpage
- **Function**: Fetch the main content from a web page.
- **Parameters**: query (string, content to search for), urls (array of strings, URLs to fetch).
- **Outputs**: Webpage content or error if fetch fails.

### github_repo
- **Function**: Search a GitHub repository for relevant source code snippets.
- **Parameters**: query (string, search query), repo (string, 'owner/repo' format).
- **Outputs**: Relevant code snippets or error if repo/query invalid.

### open_browser_page
- **Function**: Open a new browser page in the integrated browser at a given URL.
- **Parameters**: url (string, full URL).
- **Outputs**: Success confirmation or error if browser unavailable.

## VS Code Integration

### run_vscode_command
- **Function**: Run a command in VS Code.
- **Parameters**: args (array of strings, optional), commandId (string), name (string, description), skipCheck (boolean, optional).
- **Outputs**: Command result or error if command fails.

### install_extension
- **Function**: Install an extension in VS Code.
- **Parameters**: id (string, 'publisher.extension'), name (string, description).
- **Outputs**: Success confirmation or error if installation fails.

### vscode_askQuestions
- **Function**: Ask the user a series of clarifying questions.
- **Parameters**: questions (array of objects with header, question, etc.).
- **Outputs**: User responses or error if interaction fails.

### vscode_listCodeUsages
- **Function**: Find all usages of a code symbol across the workspace.
- **Parameters**: filePath (string, optional), lineContent (string), symbol (string), uri (string, optional).
- **Outputs**: List of usages or error if symbol not found.

### vscode_renameSymbol
- **Function**: Rename a code symbol across the workspace.
- **Parameters**: filePath (string, optional), lineContent (string), newName (string), symbol (string), uri (string, optional).
- **Outputs**: Success confirmation or error if rename fails.

### vscode_searchExtensions_internal
- **Function**: Search VS Code Extensions Marketplace.
- **Parameters**: category (string, optional), ids (array of strings, optional), keywords (array of strings, optional).
- **Outputs**: List of matching extensions or error if search fails.

### get_vscode_api
- **Function**: Get comprehensive VS Code API documentation.
- **Parameters**: query (string, API-related query).
- **Outputs**: API documentation or error if query invalid.

## Project and Workspace Setup

### create_new_workspace
- **Function**: Create complete project structures in a VS Code workspace.
- **Parameters**: query (string, description of the workspace).
- **Outputs**: Success confirmation with setup details or error if creation fails.

### get_project_setup_info
- **Function**: Provide project setup information for a given type and language.
- **Parameters**: projectType (string, e.g., 'python-script').
- **Outputs**: Setup instructions or error if type unsupported.

## Memory and Persistence

### memory
- **Function**: Manage persistent memory across conversations and workspaces.
- **Parameters**: command (string: 'view', 'create', etc.), various optional params like file_text, path, etc.
- **Outputs**: Memory content, success confirmation, or error based on command.

## Error and Testing

### get_errors
- **Function**: Get compile or lint errors in specific files or across all files.
- **Parameters**: filePaths (array of strings, optional).
- **Outputs**: List of errors or error if no files specified and none found.

### test_failure
- **Function**: Include test failure information in the prompt.
- **Parameters**: None.
- **Outputs**: Test failure details.

## Miscellaneous

### get_search_view_results
- **Function**: Get results from the search view.
- **Parameters**: None.
- **Outputs**: Search results.

### renderMermaidDiagram
- **Function**: Render a Mermaid diagram from markup.
- **Parameters**: markup (string, diagram markup), title (string, optional).
- **Outputs**: Rendered diagram or error if markup invalid.

### runSubagent
- **Function**: Launch a new agent to handle complex tasks autonomously.
- **Parameters**: agentName (string, optional), description (string), prompt (string).
- **Outputs**: Agent's final report or error if agent unavailable.