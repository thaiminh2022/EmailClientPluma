# EmailClientPluma
An email client (like Gmail, Outlook) for a school project.

## Developing
### Dependencies

- git
- c# with WPF
- Visual Studio

### Cloning the project

```bash
git clone https://github.com/thaiminh2022/EmailClientPluma.git
```
After cloning the project, check out the development branch
```bash
cd EmailClientPluma
git checkout dev
```
### Adding features

To start developing, create a branch of your feature through the GUI or through commands, then check out the branch you just created
```bash
git checkout -b <MY-BRANCH-NAME>
```
Happy developing!!! 😏

### Uploading your branch (Create a pull request)

- When you finish developing, create a pull request to the GitHub repository for code review
>[!IMPORTANT]
>PLEASE CREATE A PULL REQUEST TO THE DEV BRANCH

>[!Note]
>Make sure you are in your branch and commit before pushing

#### Committing
```bash
git add .
git commit -m "QUICK_DESCRIPTION"
```

#### Pull request
```bash
git push origin <YOUR_BRANCH>
```
- Go to the GitHub repo and create a pull request to the DEV branch
- Notify __me (thaiminh2022)__ when you do so.

### (optional) Resolve merge conflict

- Your code may conflict with others 
- Resolve manually in your editor or through GitHub GUI

## Authorization feature

- Create a secret folder in the project's root, inside which put the secret.json file
- Get your secret.json on the team's Google Drive
