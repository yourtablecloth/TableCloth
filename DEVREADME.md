# TableCloth Development Environment Requirements

TableCloth is optimized for development on Windows 11 or later. Full application development, including building and running the project, requires a Windows environment. On other operating systems, development is limited to specific tasks such as documentation editing and catalog management.

## Windows Development

For full development, including building and running the application, a Windows 11 or later environment is required.

### Steps for Windows

1. Clone the repository:

    ```powershell
    git clone https://github.com/yourtablecloth/TableCloth.git
    ```

2. Install Visual Studio 2026 or later.
3. Open the solution file `TableCloth.slnx` in Visual Studio.
4. Build and run the project:
    - Press `Ctrl + F5` to run without debugging.

## Visual Studio Code

> C# Dev Kit may requires a valid Visual Studio license.

1. Clone the repository:

    ```powershell
    git clone https://github.com/yourtablecloth/TableCloth.git
    ```

2. Install the C# Dev Kit extension:
    - Extension ID: `ms-dotnettools.csdevkit`
3. Build and run the project:
    - Press `Ctrl + F5`.

## Non-Windows OS Development

On macOS or Linux, development is restricted to tasks like documentation editing and catalog management. Full application build and execution are not supported.
