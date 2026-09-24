@echo off
rem Локальный тестовый запуск на Windows. Положите файл в папку сервера (рядом с ragemp-server.exe).
rem Пароль лучше задать здесь, а не в settings\mainDB.json, чтобы он не попал в git.
set REDAGE_DB_HOST=127.0.0.1
set REDAGE_DB_NAME=ra3_main
set REDAGE_DB_USER=root
set REDAGE_DB_PASSWORD=
set REDAGE_REDIS_HOST=127.0.0.1:6379
ragemp-server.exe
pause
