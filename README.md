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

* **How it looks:** [Download MongoDB v8.3.2 (MSI)](https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-8.3.2-signed.msi)

---

### 2. Put it in an Installation Guide (Recommended)
It is always best to give the user context. You can copy and paste this standard installation section into your README:

```markdown
## 🛠️ Installation Setup

### Prerequisites
Before running the project, you need to install MongoDB on your Windows machine:

1. **Download:** Click here to [Download MongoDB v8.3.2 (.msi)](https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-8.3.2-signed.msi).
2. **Install:** Run the `.msi` installer and follow the setup wizard (Choosing the **"Complete"** setup type is recommended).
3. **Service:** Make sure to check **"Install MongoDB as a Service"** so it runs automatically in the background.

> 💡 **Tip:** We highly recommend downloading [MongoDB Compass](https://www.mongodb.com/try/download/compass) as well for a GUI to manage your database easily.

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

## 📂 Project Structure

- **`src/input.css`**: The core entry point for Tailwind v4. All global overrides and custom themes (colors, typography) belong here inside the `@theme` directive.
- **`wwwroot/css/site.css`**: The generated, minified style sheet used by the application layout (`_Layout.cshtml`). **Do not modify this file manually.**
