# Як отримати готову portable-версію через GitHub Actions

Репозиторій містить workflow `.github/workflows/build-windows.yml`. Він збирає WPF-застосунок на Windows runner GitHub, публікує **self-contained win-x64** (один EXE + залежності) і пакує все в ZIP. На вашому ПК .NET SDK **не потрібен**.

## Крок 1. Створити репозиторій

1. Увійдіть у [GitHub](https://github.com) → **New repository**.
2. Назва, наприклад: `DocumentGenerator`. Можна **Private**.
3. **Не** додавайте README / .gitignore / license (репозиторій має бути порожнім).
4. Створіть репозиторій.

## Крок 2. Завантажити source

1. Розпакуйте архів `DocumentGenerator-v0_5_4-source.zip`.
2. У корінь репозиторію мають потрапити файли **зсередини** папки `DocumentGenerator/`:
   - `DocumentGenerator.csproj`
   - `MainWindow.xaml`, `App.xaml`, …
   - папка `.github/workflows/build-windows.yml`
   - `Logo.png`, `App.ico`, `samples/` тощо
3. Важливо: шлях до workflow має бути саме  
   `.github/workflows/build-windows.yml`  
   (не `DocumentGenerator/.github/...`).

### Швидко через GitHub web UI
- **Add file → Upload files** → перетягніть увесь вміст розпакованої папки → **Commit changes** у гілку `main`.

### Або через git (локально)
```bash
cd DocumentGenerator
git init
git add .
git commit -m "DocumentGenerator 0.5.4 portable source"
git branch -M main
git remote add origin https://github.com/ВАШ_НІК/DocumentGenerator.git
git push -u origin main
```

## Крок 3. Запустити збірку

1. Відкрийте вкладку **Actions** у репозиторії.
2. Якщо GitHub попросить — натисніть **I understand my workflows, go ahead and enable them**.
3. Ліворуч виберіть **Build portable Windows app**.
4. **Run workflow** → **Run workflow** (гілка `main`).
5. Дочекайтеся зеленого статусу **Success** (зазвичай 2–4 хв).

Після кожного push у `main` зі змінами `.cs` / `.xaml` / `.csproj` workflow також запускається автоматично.

## Крок 4. Завантажити portable ZIP

1. Відкрийте завершений запуск workflow.
2. Внизу сторінки в блоці **Artifacts** завантажте:
   `DocumentGenerator-0.5.4-portable-win-x64`
3. Розпакуйте ZIP, наприклад у `C:\Apps\DocumentGenerator`.
4. Запустіть `DocumentGenerator.exe`.

Архів **self-contained**: не потрібні .NET Runtime і Microsoft Word. Word потрібен лише якщо хочете відкривати DOCX вручну.

> Не ставте програму в `C:\Program Files` — вона пише `data`, `templates`, `exports`, `backups`, `logs` поруч із EXE і потребує права запису.

## Якщо збірка не запускається / падає

- Перевірте шлях: `.github/workflows/build-windows.yml`.
- Settings → Actions → General → дозвольте Actions.
- Відкрийте червоний job → розгорніть крок з помилкою → скопіюйте лог.

## Локальна збірка (опційно)

На Windows з встановленим [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```cmd
build-portable.cmd
```

або:

```powershell
.\publish-windows.ps1
```

Результат: `publish\win-x64\` і ZIP `publish\DocumentGenerator-0.5.4-portable-win-x64.zip`.
