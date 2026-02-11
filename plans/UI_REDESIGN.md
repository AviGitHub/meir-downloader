# UI Redesign Plan: MeirDownloader

## 1. Design Philosophy
- **Style:** Modern Minimalist (Flat 2.0)
- **Direction:** Right-to-Left (RTL) Native
- **Vibe:** Professional, Serene, Focused
- **Inspiration:** Modern Windows 11 apps, Notion, Spotify

## 2. Color Palette
### Light Theme (Default)
| Role | Color | Hex | Usage |
|------|-------|-----|-------|
| **Primary** | Ocean Blue | `#0F172A` | Sidebar background, Primary headers |
| **Accent** | Meir Green | `#10B981` | Action buttons, Success states, Progress bars |
| **Secondary Accent** | Sky Blue | `#3B82F6` | Links, Selection highlights |
| **Background** | Off-White | `#F1F5F9` | Main app background |
| **Surface** | White | `#FFFFFF` | Cards, Content panels |
| **Text Primary** | Slate Dark | `#1E293B` | Main text |
| **Text Secondary** | Slate Grey | `#64748B` | Subtitles, Metadata |
| **Error** | Rose | `#EF4444` | Error messages, Cancel actions |
| **Warning** | Amber | `#F59E0B` | Alerts |

## 3. Typography
- **Font Family:** `Segoe UI Variable Display` (if available), fallback to `Segoe UI`.
- **Weights:**
  - **Bold (700):** Headers, Active states
  - **SemiBold (600):** Button text, Column headers
  - **Regular (400):** Body text
- **Sizes:**
  - **H1 (App Title):** 24px
  - **H2 (Section Title):** 20px
  - **H3 (Card Title):** 16px
  - **Body:** 14px
  - **Caption:** 12px

## 4. Layout Architecture (RTL)
The layout will use a standard "Master-Detail" approach but reversed for Hebrew.

```mermaid
graph TD
    Window[Main Window]
    Window --> TopBar[Top Bar (Settings, Path)]
    Window --> Body[Main Body Grid]
    Body --> Sidebar[Right Sidebar (Rabbis)]
    Body --> Content[Left Content Area]
    Content --> Series[Series Selection (Top)]
    Content --> Lessons[Lessons List (Bottom/Fill)]
    Window --> StatusBar[Bottom Status Bar]
```

### Structure
1.  **Right Sidebar (300px):**
    *   Search Box (Top)
    *   Rabbi List (VirtualizingStackPanel)
    *   Each item: Avatar circle (initials), Name, Lesson count badge.
2.  **Main Content (Star):**
    *   **Header:** Breadcrumbs (Rabbi > Series), "Download All" actions.
    *   **Series Selector (Horizontal Scroll or Grid):**
        *   Cards displaying Series Name and Count.
        *   Selected state has high contrast border/shadow.
    *   **Lessons List (DataGrid):**
        *   Heavily styled DataGrid.
        *   No vertical grid lines.
        *   Row hover effects.
        *   Columns: #, Title, Date, Duration, Status (Progress), Action.
3.  **Top Bar (Auto):**
    *   App Logo/Title (Right).
    *   Download Folder Path selector (Left).
4.  **Status Bar (Auto):**
    *   Global Progress Bar (Slim).
    *   Status Text.

## 5. Component Designs

### Rabbi Card (Sidebar Item)
- **Container:** Border with CornerRadius="8", Padding="10".
- **State:** Transparent background normally, `#E2E8F0` on hover, `#3B82F6` (with White text) when selected.
- **Content:** Horizontal StackPanel.
  - Circle Border (40x40) with Initials.
  - TextBlock (Name) VerticalAlignment="Center".
  - Pill Border (Count) aligned to left.

### Series Card
- **Container:** Border, Background="White", CornerRadius="12", DropShadow.
- **Size:** ~200px width, ~100px height.
- **Content:**
  - Title (TextWrapping).
  - Badge (Count).
- **Interaction:** Scale transform on hover (1.02x).

### Lessons DataGrid
- **Headers:** Transparent background, Bold text, Bottom border only.
- **Rows:** Alternating colors optional (or just hover effect).
- **Progress Bar:** Rounded corners (CornerRadius="4"), Height="6".
- **Action Button:** Circular or Pill-shaped, Icon-only or "Download" text.

### Buttons
- **Primary:** Background="{StaticResource AccentColor}", Foreground="White", CornerRadius="6".
- **Secondary:** BorderThickness="1", BorderBrush="{StaticResource AccentColor}", Background="Transparent".

## 6. Animations & Transitions
- **Loading:** Skeleton loader or Shimmer effect instead of simple spinner if possible, otherwise a modern circular spinner.
- **Lists:** `EntranceThemeTransition` for lists loading.
- **Hover:** `ColorAnimation` on backgrounds.

## 7. Implementation Strategy
1.  **Resources:** Create `Styles.xaml` and `Colors.xaml` in `Resources` folder.
2.  **Converters:** Ensure `BooleanToVisibility`, `StatusToColor` are robust.
3.  **Icons:** Use `Segoe Fluent Icons` (font) or SVG paths for vector icons (Download, Folder, Settings, Play, Pause).

## 8. Proposed XAML Structure (High Level)

```xml
<Window ... FlowDirection="RightToLeft" Background="{StaticResource BackgroundBrush}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" /> <!-- Main Content -->
            <ColumnDefinition Width="320" /> <!-- Sidebar (Right) -->
        </Grid.ColumnDefinitions>

        <!-- Right Sidebar: Rabbis -->
        <Border Grid.Column="1" Background="White" Effect="{StaticResource ShadowEffect}">
            <Grid Margin="20">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" /> <!-- Title/Search -->
                    <RowDefinition Height="*" />    <!-- List -->
                </Grid.RowDefinitions>
                <!-- Search Box -->
                <!-- Rabbi ListBox with ItemTemplate -->
            </Grid>
        </Border>

        <!-- Main Content -->
        <Grid Grid.Column="0" Margin="30">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" /> <!-- Top Bar / Breadcrumbs -->
                <RowDefinition Height="Auto" /> <!-- Series Horizontal List -->
                <RowDefinition Height="*" />    <!-- Lessons DataGrid -->
                <RowDefinition Height="Auto" /> <!-- Bottom Actions / Status -->
            </Grid.RowDefinitions>

            <!-- Top Bar -->
            <StackPanel Grid.Row="0" Orientation="Horizontal">
                <!-- Folder Picker -->
            </StackPanel>

            <!-- Series Selection -->
            <ScrollViewer Grid.Row="1" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                <ItemsControl x:Name="SeriesList" ... />
            </ScrollViewer>

            <!-- Lessons List -->
            <DataGrid Grid.Row="2" Style="{StaticResource ModernDataGridStyle}" ... />
            
            <!-- Global Progress -->
            <Grid Grid.Row="3">
                <!-- Progress Bar and Status Text -->
            </Grid>
        </Grid>
    </Grid>
</Window>
```
