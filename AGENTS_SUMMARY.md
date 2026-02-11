# MeirDownloader Project Summary

## 1. Project Overview
MeirDownloader is a WPF desktop application built with .NET Core designed to browse and download Torah lessons from the Meir TV website (meirtv.com). It provides a user-friendly interface for selecting rabbis, series, and lessons, and manages concurrent downloads efficiently.

## 2. Architecture
The solution follows a clean architecture pattern:
*   **MeirDownloader.Core**: Contains the business logic, models, and service interfaces. It handles API communication with the WordPress backend.
*   **MeirDownloader.Desktop**: The WPF presentation layer (MVVM pattern). It handles UI logic, view models, and local system interactions (file system, settings).
*   **MeirDownloader.Api**: (Optional/Support) Contains API-related definitions or proxy logic if applicable.

## 3. Optimizations Implemented
*   **Concurrency**:
    *   Utilizes `SemaphoreSlim` to limit concurrent HTTP requests (default: 6) when fetching paginated data (rabbis, series).
    *   Uses `Task.WhenAll` to process multiple pages or chunks of data in parallel.
*   **Resilience**:
    *   Implements `Polly` retry policies with exponential backoff for HTTP requests to handle transient failures (5xx errors, 429 Too Many Requests).
*   **Robustness**:
    *   Global exception handling configured in `App.xaml.cs` covering `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, and `AppDomain.UnhandledException`.
    *   Logs fatal errors to a local file (`meir-downloader-errors.log`).
*   **Speed**:
    *   `DownloadManager` manages concurrent file downloads with a semaphore.
    *   Optimized file I/O using large buffers (81KB) and `FileOptions.Asynchronous`.
    *   UI progress updates are throttled (200ms interval) to prevent UI freezing during high-speed downloads.
*   **Caching**:
    *   Implemented `LiteDbCacheService` using `LiteDB` to cache API responses (Rabbis, Series, Lessons) locally.
    *   Reduces API calls on subsequent launches and improves startup time.
*   **User Experience**:
    *   Added loading indicators for Rabbi images in `MainWindow.xaml` (bound to `RabbiViewModel.IsImageLoading`) to provide visual feedback during asynchronous image fetching.
    *   **Search Functionality**: Fixed search functionality in `MainWindow.xaml` to ensure responsive and accurate filtering.
*   **Logic Improvements**:
    *   **Rabbi Filtering**: Implemented filtering in `MainWindow.xaml.cs` to exclude Rabbis with 0 lessons or series from the display.

## 4. Key Files
*   **`MeirDownloader.Core/Services/MeirDownloaderService.cs`**: Core service for fetching data from the WordPress API. Implements the retry logic, caching integration, and concurrent fetching strategies.
*   **`MeirDownloader.Core/Services/LiteDbCacheService.cs`**: Implements `ICacheService` using LiteDB for persistent local caching of API responses.
*   **`MeirDownloader.Core/Services/ICacheService.cs`**: Interface defining the caching contract (Get, Set, Clear).
*   **`MeirDownloader.Desktop/Services/DownloadManager.cs`**: Manages the download queue, concurrency limits, and progress reporting. Handles file creation and cleanup on failure.
*   **`MeirDownloader.Desktop/App.xaml.cs`**: Entry point for the desktop application. Configures global exception handlers and logging.
*   **`MeirDownloader.Core/Models/Lesson.cs`**: Data model representing a single lesson, including metadata and download links.

## 5. Future Considerations
*   **UI Virtualization**: Implement UI virtualization for lists with large numbers of items (e.g., lessons) to improve rendering performance.
*   **Search Functionality**: Add full-text search for lessons and series.
