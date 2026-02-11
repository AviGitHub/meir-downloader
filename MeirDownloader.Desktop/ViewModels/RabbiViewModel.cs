using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using MeirDownloader.Core.Models;

namespace MeirDownloader.Desktop.ViewModels;

public class RabbiViewModel : INotifyPropertyChanged
{
    private BitmapImage? _avatarImage;
    private bool _imageLoaded;
    private bool _isImageLoading;

    public Rabbi Rabbi { get; }

    public string Id => Rabbi.Id;
    public string Name => Rabbi.Name;
    public int Count => Rabbi.Count;
    public string Link => Rabbi.Link;
    public string ImageUrl => Rabbi.ImageUrl;

    public BitmapImage? AvatarImage
    {
        get => _avatarImage;
        set { _avatarImage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasImage)); }
    }

    public bool HasImage => _avatarImage != null;
    public bool ImageLoaded => _imageLoaded;

    public bool IsImageLoading
    {
        get => _isImageLoading;
        set { _isImageLoading = value; OnPropertyChanged(); }
    }

    public RabbiViewModel(Rabbi rabbi)
    {
        Rabbi = rabbi;
    }

    public void MarkImageLoaded()
    {
        _imageLoaded = true;
        OnPropertyChanged(nameof(ImageLoaded));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
