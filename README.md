# 📧 EmailClientPluma

An email client (similar to Gmail or Outlook) built with **C# and WPF** for a **school project**.

---

## 📑 Table of Contents

* [For Professor](#-for-professor)

  * [Project Objective](#-project-objective)
  * [Key Features](#-key-features)
  * [Technologies Used](#-technologies-used)
  * [How to Run](#️-how-to-run)
  * [Application Data Locations](#-application-data-locations)
* [For Developers](#-for-developers)

  * [Prerequisites](#-prerequisites)
  * [Clone & Setup](#-clone--setup)
  * [Development Workflow](#-development-workflow)
  * [Secrets Configuration](#-secrets-configuration)
  * [Merge Conflicts](#-merge-conflicts)

---

## 👨‍🏫 For Professor

> **Important:** Since this application is built with **WPF**, please test it on a **Windows environment**.

### 🎯 Project Objective

**EmailClientPluma** is a desktop-based email client designed to demonstrate:

* Understanding of **desktop application development** using WPF
* Application of the **MVVM architectural pattern**
* Integration with **email provider APIs** (e.g., Google, Microsoft)
* Email phishing detection using **Levenshtein distance**

---

### 🧩 Key Features

* Multiple email account support
* Send, receive, and read emails
* Attachment handling (add, view, delete)
* Local data persistence using **SQLite**
* Basic error handling and logging
* Email phishing detection

---

### 🛠 Technologies Used

* **Language:** C# (.NET)
* **UI Framework:** WPF
* **Architecture:** MVVM
* **Database:** SQLite
* **Version Control:** Git & GitHub

---

### ▶️ How to Run

1. Open the solution in **Visual Studio**
2. Restore all NuGet packages
3. Configure the required `secret.json` file (see *For Developers* section)

   * If you are running the **ZIP version provided via Google Drive**, the `secret.json` file is already included
4. Build and run the project

---

### 📂 Application Data Locations

* **Database & logs:**
  `%AppData%/Pluma`
  `%AppData%/Pluma/log`

* **Downloaded attachments:**
  `Documents/PlumaAttachment`

---

## 👨‍💻 For Developers

### 🔧 Prerequisites

* Git
* Visual Studio 2022 or later (with WPF workload)
* .NET SDK

---

### 📦 Clone & Setup

```bash
git clone https://github.com/thaiminh2022/EmailClientPluma.git
cd EmailClientPluma
git checkout dev
```

---

### 🌱 Development Workflow

Create a feature branch from `dev`:

```bash
git checkout -b <your-branch-name>
```

Commit and push your changes:

```bash
git add .
git commit -m "Short, descriptive commit message"
git push origin <your-branch-name>
```

Create a **Pull Request targeting the `dev` branch** and notify **@thaiminh2022** for review.

---

### 🔐 Secrets Configuration

This project uses private credentials that are **not tracked by Git**.
Although these credentials are used in a desktop application, this setup follows Google’s recommended approach for installed apps.

Create the following directory structure:

```
EmailClientPluma/
├─ secrets/
│  └─ secret.json
```

Obtain `secret.json` from the team’s **Google Drive**.

> ⚠️ **Never commit `secret.json` to the repository**

---

### ⚔️ Merge Conflicts

If merge conflicts occur:

* Resolve them locally using **Visual Studio** or another Git client
* Alternatively, resolve conflicts via the **GitHub web interface**
* Commit the resolved changes and push again

---

Happy developing 😏
