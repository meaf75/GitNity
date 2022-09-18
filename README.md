# UniGit
Git integration for Unity projects

# Features
- [x] List changes (Modified, untracked, new, merge error)
- [x] Commit selected files
- [x] Push pending commits
- [x] Fetch changes
- [x] Create & switch branch
- [ ] Context Menu on right click on an asset
- [ ] Display status icon on files with modifications or tracked by git

# Git commands used
Here is a list of commands used to populate window data or execute some git operations

- Get current branch
    ``` sh
    git symbolic-ref --short HEAD
    ```

- Get all branches
    ``` sh
    git branch -a --no-color
    ```

- Get ref paths
    ``` sh
    git for-each-ref --sort -committerdate --format "%(refname) %(objectname) %(*objectname)"
    ```

- Revert file
    ``` sh
    git clean -f -q -- "PATH"
    ```

- Diff file
    ``` sh
    git diff -U$(wc -l Assets/Plugins/UniGit/Editor/UniGit.cs)
    ```

- Get commits
    ``` sh
    git log --format="%H %h %an %ae %ai %s" --max-count=301 --date-order master --
    ```

- Stage files
    ``` sh
    git add -A -- FILE_OR_FILES_PATH
    ```

- Commit staged files
    ``` sh
    git commit -m MESSAGE_TEXT
    ```

- Push commits
    ``` sh
    git push
    ```

    ``` sh
    git push -u ORIGIN_NAME BRANCH_NAME
    ```

- Checkout to branch
    ``` sh
    git checkout  BRANCH_NAME
    ```
  
- Check if branch exist
    ``` sh
    git rev-parse --verify BRANCH_NAME
    ```

- Create branch
    ``` sh
    git checkout -b BRANCH_NAME FROM_BRANCH_NAME
    ```
  
    ``` sh
    git branch BRANCH_NAME FROM_BRANCH_NAME
    ```