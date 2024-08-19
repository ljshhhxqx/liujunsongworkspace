@echo off

:loop
REM 获取当前时间并输出到命令行
echo [%date% %time%] Checking for updates...

REM Fetch the latest changes from the remote repository
git fetch origin

REM 检查本地和远程分支的状态
git status -uno

REM 检查本地分支是否落后于远程分支
git rev-list --count HEAD..origin/main >nul 2>&1
if %errorlevel% equ 0 (
    echo [%date% %time%] Pulling latest changes...
    
    REM 尝试合并更改，如果发生冲突则重置为远程版本
    git pull origin master
    if %errorlevel% neq 0 (
        echo [%date% %time%] Merge conflict detected. Stashing local changes...
        
        REM 暂存本地更改
        git stash save "Temporary stash before resetting to remote"
        
        echo [%date% %time%] Resetting to remote version...
        git reset --hard origin/master
        
        REM 恢复暂存的更改
        git stash pop
    )
) else (
    echo [%date% %time%] No updates found. Waiting for the next check...
)

REM Sleep for a specified interval (e.g., 300 seconds or 5 minutes)
timeout /t 35 /nobreak >nul

REM 每隔一段时间输出等待状态到命令行
for /L %%i in (1,1,5) do (
    echo [%date% %time%] Waiting for the next check...
    timeout /t 30 /nobreak >nul  REM 每60秒输出一次等待状态
)

REM Loop back to the start to check again
goto loop
