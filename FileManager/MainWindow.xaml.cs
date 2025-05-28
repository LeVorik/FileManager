using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace FileManager
{
    public class FileItem
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string DateModified { get; set; }
    }

    public partial class MainWindow : Window
    {
        private DirectoryInfo _currentDirectory;
        private readonly Stack<DirectoryInfo> _backHistory = new Stack<DirectoryInfo>();
        private readonly Stack<DirectoryInfo> _forwardHistory = new Stack<DirectoryInfo>();
        private string _clipboardPath = null;
        private bool _isCutOperation = false; // Можно для расширения (вырезать/копировать)


        public MainWindow()
        {
            InitializeComponent();
            LoadDrives();
        }

        private void LoadDrives()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                var item = new TreeViewItem { Header = drive.Name, Tag = drive.RootDirectory };
                item.Items.Add(null);
                item.Expanded += Folder_Expanded;
                DirectoryTree.Items.Add(item);
            }
        }

        private void Folder_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] == null)
            {
                item.Items.Clear();
                var dir = (DirectoryInfo)item.Tag;

                try
                {
                    foreach (var subDir in dir.GetDirectories())
                    {
                        if (!ShowHiddenCheckBox.IsChecked.GetValueOrDefault() && (subDir.Attributes & FileAttributes.Hidden) != 0)
                            continue;

                        var subItem = new TreeViewItem
                        {
                            Header = subDir.Name,
                            Tag = subDir
                        };
                        subItem.Items.Add(null);
                        subItem.Expanded += Folder_Expanded;
                        item.Items.Add(subItem);
                    }
                }
                catch { }
            }
        }

        private void DirectoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = (TreeViewItem)DirectoryTree.SelectedItem;
            if (selectedItem?.Tag is DirectoryInfo dir)
            {
                NavigateToDirectory(dir);
            }
        }

        private void NavigateToDirectory(DirectoryInfo dir)
        {
            if (_currentDirectory != null)
            {
                _backHistory.Push(_currentDirectory);
                _forwardHistory.Clear();
            }
            LoadDirectoryContent(dir);
        }

        private void LoadDirectoryContent(DirectoryInfo dir)
        {
            _currentDirectory = dir;
            FileList.Items.Clear();

            try
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    if (!ShowHiddenCheckBox.IsChecked.GetValueOrDefault() && (subDir.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    FileList.Items.Add(new FileItem
                    {
                        Name = subDir.Name,
                        Type = "Папка",
                        Size = "",
                        DateModified = subDir.LastWriteTime.ToString()
                    });
                }

                foreach (var file in dir.GetFiles())
                {
                    if (!ShowHiddenCheckBox.IsChecked.GetValueOrDefault() && (file.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    FileList.Items.Add(new FileItem
                    {
                        Name = file.Name,
                        Type = file.Extension,
                        Size = (file.Length / 1024).ToString(),
                        DateModified = file.LastWriteTime.ToString()
                    });
                }
            }
            catch { }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_backHistory.Count > 0)
            {
                _forwardHistory.Push(_currentDirectory);
                var previousDir = _backHistory.Pop();
                LoadDirectoryContent(previousDir);
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_forwardHistory.Count > 0)
            {
                _backHistory.Push(_currentDirectory);
                var nextDir = _forwardHistory.Pop();
                LoadDirectoryContent(nextDir);
            }
        }

        private void ShowHiddenCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_currentDirectory != null)
                LoadDirectoryContent(_currentDirectory);
        }

        private void FileList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected && _currentDirectory != null)
            {
                string path = Path.Combine(_currentDirectory.FullName, selected.Name);
                if (Directory.Exists(path))
                {
                    NavigateToDirectory(new DirectoryInfo(path));
                }
                else if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected && _currentDirectory != null)
            {
                string path = Path.Combine(_currentDirectory.FullName, selected.Name);
                if (Directory.Exists(path))
                {
                    NavigateToDirectory(new DirectoryInfo(path));
                }
                else if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected && _currentDirectory != null)
            {
                string path = Path.Combine(_currentDirectory.FullName, selected.Name);
                try
                {
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                    LoadDirectoryContent(_currentDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка удаления: " + ex.Message);
                }
            }
        }

        private void RenameItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected && _currentDirectory != null)
            {
                string oldPath = Path.Combine(_currentDirectory.FullName, selected.Name);

                // Простой диалог переименования
                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите новое имя:", "Переименование", selected.Name);

                if (!string.IsNullOrWhiteSpace(input) && input != selected.Name)
                {
                    string newPath = Path.Combine(_currentDirectory.FullName, input);

                    try
                    {
                        if (Directory.Exists(oldPath))
                            Directory.Move(oldPath, newPath);
                        else if (File.Exists(oldPath))
                            File.Move(oldPath, newPath);

                        LoadDirectoryContent(_currentDirectory);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка переименования: " + ex.Message);
                    }
                }
            }
        }

        private void PropertiesItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected)
            {
                MessageBox.Show($"Имя: {selected.Name}\nТип: {selected.Type}\nРазмер: {selected.Size}\nИзменён: {selected.DateModified}");
            }
        }

        private void CopyItem_Click(object sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem selected && _currentDirectory != null)
            {
                _clipboardPath = Path.Combine(_currentDirectory.FullName, selected.Name);
                _isCutOperation = false; // Просто копирование
            }
        }

        private void PasteItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_clipboardPath) || _currentDirectory == null)
                return;

            try
            {
                var destinationPath = Path.Combine(_currentDirectory.FullName, Path.GetFileName(_clipboardPath));

                if (Directory.Exists(_clipboardPath))
                {
                    CopyDirectory(_clipboardPath, destinationPath);
                }
                else if (File.Exists(_clipboardPath))
                {
                    File.Copy(_clipboardPath, destinationPath, overwrite: true);
                }

                LoadDirectoryContent(_currentDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка вставки: " + ex.Message);
            }
        }
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}
