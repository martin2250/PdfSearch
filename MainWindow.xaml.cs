using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.IO;
using System.Windows.Media.Animation;
using PdfExtract;
using System.Text.RegularExpressions;
using System.Threading;

namespace PdfSearch
{
	public class PdfFileInfo
	{
		public string Path { get; set; }
		public string FileName { get; set; }
		public string IndexedCharacters { get; set; }
		public string Size { get; set; }
		public string Content { get; set; }
	}

	public partial class MainWindow : Window
	{
		FolderBrowserDialog folderBrowser = new FolderBrowserDialog();

		OpenFileDialog ofd = new OpenFileDialog() { Filter = "Index Files|*.index" };
		SaveFileDialog sfd = new SaveFileDialog() { Filter = "Index Files|*.index" };

		Storyboard storyBoardAnimateInfo = new Storyboard();

		List<PdfFileInfo> Files = new List<PdfFileInfo>();

		Task taskFilter;
		CancellationTokenSource cancelFilter;

		public MainWindow()
		{
			InitializeComponent();

			folderBrowser.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			textBoxPath.Text = folderBrowser.SelectedPath;

			ofd.FileOk += Ofd_FileOk;
			sfd.FileOk += Sfd_FileOk;

            var a = new DoubleAnimation
			{
				From = 1.0,
				To = 0.0,
				FillBehavior = FillBehavior.Stop,
				BeginTime = TimeSpan.FromSeconds(3),
				Duration = new Duration(TimeSpan.FromSeconds(0.5))
			};
			storyBoardAnimateInfo.Children.Add(a);
			Storyboard.SetTarget(a, statusBarItemInfo);
			Storyboard.SetTargetProperty(a, new PropertyPath(OpacityProperty));
			storyBoardAnimateInfo.Completed += delegate { statusBarItemInfo.Visibility = Visibility.Hidden; };

		}

		private void ShowInfo(string info)
		{
			statusBarItemInfo.Content = info;
			statusBarItemInfo.Visibility = Visibility.Visible;
			storyBoardAnimateInfo.Stop();
			storyBoardAnimateInfo.Begin();
		}

		private void buttonBrowse_Click(object sender, RoutedEventArgs e)
		{
			folderBrowser.ShowDialog();
			textBoxPath.Text = folderBrowser.SelectedPath;
		}

		private void CreateIndex(string path)
		{
			Files.Clear();

			string[] files = Directory.GetFiles(path, "*.pdf", SearchOption.AllDirectories);

			foreach(string file in files)
			{
				FileInfo fi = new FileInfo(file);

				PdfFileInfo info = new PdfFileInfo();

				info.Path = file;
				info.FileName = fi.Name;
				info.Size = $"{fi.Length /1024}k";

				try
				{ 
					using (var pdfStream = File.OpenRead(file))
					using (var extractor = new Extractor())
					{
						info.Content = extractor.ExtractToString(pdfStream).ToLower();
						info.IndexedCharacters = info.Content.Length.ToString();
						Files.Add(info);
					}
				}
				catch { }
			}
		}

		private async void buttonCreateIndex_Click(object sender, RoutedEventArgs e)
		{
			string path = textBoxPath.Text;
            if (!Directory.Exists(path))
			{
				ShowInfo("Selected Directory does not exist!");
				return;
			}

			ellipseLoading.Visibility = Visibility.Visible;
			gridMain.IsEnabled = false;

			await Task.Run(()=>CreateIndex(path));

			statusIndexed.Content = Files.Count.ToString();

			Filter();

			ellipseLoading.Visibility = Visibility.Hidden;
			gridMain.IsEnabled = true;
		}

		Regex patternParser = new Regex("(\\\"[^\\\"]+\\\"|[\\S]+)");

		private void Filter_Work(string pattern)
		{
			List<Regex> filters = new List<Regex>();

			foreach (Match filter in patternParser.Matches(pattern))
			{
				filters.Add(new Regex(Regex.Escape(filter.Value.Trim('"')).Replace("\\?", ".?").Replace("\\*", ".*")));
			}

			Dispatcher.Invoke(()=>listView.Items.Clear());
			
			foreach (PdfFileInfo info in Files)
			{
				if (cancelFilter.Token.IsCancellationRequested)
					return;

				if (!filters.Any())
				{
					Dispatcher.Invoke(() => listView.Items.Add(info));
					Dispatcher.Invoke(() => statusMatched.Content = listView.Items.Count.ToString());
					continue;
				}

				bool ok = true;

				foreach (Regex filter in filters)
				{
					if (cancelFilter.Token.IsCancellationRequested)
						return;

					if (!filter.IsMatch(info.Content))
					{
						ok = false;
						break;
					}
				}

				if (ok)
				{
					Dispatcher.Invoke(() => listView.Items.Add(info));
					Dispatcher.Invoke(() => statusMatched.Content = listView.Items.Count.ToString());
				}
			}

			taskFilter = null;
		}

		private async void Filter()
		{
			if(taskFilter != null)
			{
				cancelFilter.Cancel();
				await taskFilter;
			}

			string pattern = textBoxFilter.Text.ToLower();

			cancelFilter = new CancellationTokenSource();
			taskFilter = new Task(()=>Filter_Work(pattern));
			taskFilter.Start();
		}

		private void listView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			PdfFileInfo i = listView.SelectedItem as PdfFileInfo;

			if(i != null)
			{
				System.Diagnostics.Process.Start(i.Path);
			}
		}

		private void textBoxFilter_TextChanged(object sender, TextChangedEventArgs e)
		{
			Filter();
		}

		private void MenuItemLoad_Click(object sender, RoutedEventArgs e)
		{
			ofd.ShowDialog();
		}

		private void Ofd_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
		{
			try
			{
				List<PdfFileInfo> files = new List<PdfFileInfo>();

				BinaryReader r = new BinaryReader(File.OpenRead(ofd.FileName));

				int count = r.ReadInt32();

				while(count-- > 0)
				{
					PdfFileInfo info = new PdfFileInfo();
					info.Path = r.ReadString();
					info.FileName = r.ReadString();
					info.Size = r.ReadString();
					info.Content = r.ReadString();
					info.IndexedCharacters = info.Content.Length.ToString();

					files.Add(info);
				}

				r.Close();

				Files = files;

				Filter();

				ShowInfo("Loaded Index");
			}
			catch
			{
				ShowInfo("Could not read File");
			}			
		}

		private void MenuItemSave_Click(object sender, RoutedEventArgs e)
		{
			sfd.ShowDialog();
		}

		private void Sfd_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
		{
			try
			{
				BinaryWriter w = new BinaryWriter(File.OpenWrite(sfd.FileName));

				w.Write(Files.Count);

				foreach(PdfFileInfo info in Files)
				{
					w.Write(info.Path);
					w.Write(info.FileName);
					w.Write(info.Size);
					w.Write(info.Content);
				}

				w.Close();
				ShowInfo("Saved Index");
			}
			catch
			{
				ShowInfo("Could not write File");
			}
		}

		private void MenuItemOpenFolder_Click(object sender, RoutedEventArgs e)
		{
			string path = new FileInfo(((PdfFileInfo)listView.SelectedItem).Path).DirectoryName;
			System.Diagnostics.Process.Start(path);
		}

		private void MenuItemShowText_Click(object sender, RoutedEventArgs e)
		{
			string file = Path.GetTempFileName() + ".txt";

			try
			{
				File.WriteAllText(file, ((PdfFileInfo)listView.SelectedItem).Content);
				System.Diagnostics.Process.Start(file);
			}
			catch { }
		}
	}
}
