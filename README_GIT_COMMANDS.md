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

- Revert file (untracked)
    ``` sh
    git clean -f -q -- "PATH"
    ```

- Revert file (tracked)
    ``` sh
    git checkout "PATH"
    ```

- Diff file
    ``` sh
    git diff --cached --word-diff=porcelain -U9999 FILE_PATH
    ```

- Get commits
    ``` sh
    git log --format="%H #UG# %h #UG# %an #UG# %ae #UG# %ai #UG# %s" --max-count=301 --date-order master --
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
  
- Get commits behind
    ``` sh
    git status -b --porcelain=v2
    ```
  
    ``` sh
    git branch BRANCH_NAME FROM_BRANCH_NAME
    ```
  
- List changes
    ``` sh
    git status -u -s

- Set path for private ssh path
    ``` sh
    git config core.sshCommand "ssh -i PRIVATE_SSH_KEY_PATH"