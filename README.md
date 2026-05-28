# MenuQr - ASP.NET Core 9.0 MVC & Tailwind CSS v4

A comprehensive guide to setting up and developing the **MenuQr** web application using **ASP.NET Core 9.0 MVC**, **Tailwind CSS v4**, and **Yarn**.

Choose your preferred Integrated Development Environment (IDE) below and follow the complete step-by-step setup to launch the project.

---

## 🖥️ Option 1: Visual Studio Code (VS Code) Workflow

This workflow uses a single integrated terminal and the `concurrently` package to run both the .NET development server and the Tailwind CSS compiler simultaneously.

**Step-by-step Setup:**

1. Open the project root folder (`MenuQr-Web`) in VS Code.
2. Open the integrated terminal (``Ctrl + ` ``) and navigate to the source directory:
   ```bash
   cd MenuQr
   ```
3. Run the following commands sequentially to prepare the environment and start the application:

```bash
# 1. Install and use Node 20 (via NVM)
nvm install 20
nvm use 20

# 2. Install Yarn globally
npm install --global yarn

# 3. Grant script execution permissions (Required for Windows PowerShell)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# 4. Restore Backend dependencies (.NET NuGet packages)
dotnet restore

# 5. Install Frontend dependencies (Tailwind CSS & Concurrently)
yarn install

# 6. Start the unified development server
yarn dev
```

_Note: The browser will open automatically. Edits made to `.cs` or `.cshtml` files will hot-reload instantly._

 4. download MongoDB v8.3.2
* **How it looks:** [Download MongoDB v8.3.2 (MSI)](https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-8.3.2-signed.msi)

---

## 💜 Option 2: Visual Studio 2022 Workflow

This workflow utilizes Visual Studio's native build system for the backend, while keeping a separate terminal running for the frontend compiler.

**Step-by-step Setup:**

1. Open the solution file **`MenuQr.sln`** using Visual Studio 2022.
2. Open the integrated terminal inside Visual Studio (Right-click the `MenuQr` project in Solution Explorer -> **Open in Terminal**).
3. Run the following commands in the terminal to set up the frontend and backend dependencies:

```bash
# 1. Install and use Node 20 (via NVM)
nvm install 20
nvm use 20

# 2. Install Yarn globally
npm install --global yarn

# 3. Grant script execution permissions (Required for Windows PowerShell)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned

# 4. Restore Backend dependencies (.NET NuGet packages)
dotnet restore

# 5. Install Frontend dependencies (Tailwind CSS)
yarn install
```

4. **Run the Tailwind Watcher:** Keep the terminal active in the background to track real-time style changes by running:

```bash
yarn tailwind:watch
```

5. **Run the Application:** Press **F5** or **Ctrl + F5** on your keyboard to compile and run the backend .NET server. Hot-reload will be handled natively by Visual Studio.

---
 6. download MongoDB v8.3.2
* **How it looks:** [Download MongoDB v8.3.2 (MSI)](https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-8.3.2-signed.msi)


## 📄 Configuration Files (`package.json`)

Ensure your `package.json` inside the `MenuQr` directory matches this exact configuration for the commands above to work:

```json
{
  "name": "MenuQr",
  "version": "1.0.0",
  "main": "index.js",
  "keywords": [],
  "author": "AwtichDev",
  "license": "ISC",
  "scripts": {
    "tailwind:watch": "tailwindcss -i ./src/input.css -o ./wwwroot/css/site.css --watch",
    "dev": "concurrently \"yarn tailwind:watch\" \"dotnet watch\""
  },
  "devDependencies": {
    "@tailwindcss/cli": "^4.3.0",
    "concurrently": "^9.2.1",
    "tailwindcss": "^4.3.0"
  }
}
```

---
# 🐳 Docker Desktop Setup & Environment Guide

A professional, streamlined guide to installing, configuring, and verifying **Docker Desktop** on Windows using the **WSL 2** backend.

---

## 📌 Prerequisites

Before initiating the installation, ensure that hardware virtualization is active on your host system:

1. Open **Task Manager** (`Ctrl + Shift + Esc`).
2. Navigate to the **Performance** tab and select **CPU**.
3. Verify that **Virtualization** is marked as **Enabled**.

---

## 🚀 Installation Workflow

### 1. Download the Installer
Get the latest stable production build directly from the official repository:
👉 **[Download Docker Desktop Official](https://www.docker.com/products/docker-desktop/)**

### 2. Configuration Setup
* Launch `Docker Desktop Installer.exe`.
* **Crucial:** Ensure the **"Use WSL 2 instead of Hyper-V"** checkbox remains **selected** for native Linux performance.
* Complete the setup wizard and select **Close and restart** to safely reboot your operating system.

### 3. Initialization
* Launch the **Docker Desktop** GUI application.
* Click **Accept** on the Subscription Service Agreement screen.
* You may safely select **Skip** on the welcome survey to access the primary dashboard immediately.

---

## 🛠️ Troubleshooting Common Failures

### ❌ Error: "Windows Subsystem for Linux must be updated"
If the automated backend update aborts during setup, force a manual update:
1. Launch **PowerShell** or **Command Prompt** with **Administrator privileges**.
2. Execute the following system command:
```bash
   wsl --update
```

## 📂 Project Structure

- **`src/input.css`**: The core entry point for Tailwind v4. All global overrides and custom themes (colors, typography) belong here inside the `@theme` directive.
- **`wwwroot/css/site.css`**: The generated, minified style sheet used by the application layout (`_Layout.cshtml`). **Do not modify this file manually.**
