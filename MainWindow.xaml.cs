using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Media.Animation;
using System.Collections.ObjectModel;
using PdfExtract;
using System.Text.RegularExpressions;

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
		Storyboard storyBoardAnimateInfo = new Storyboard();

		List<PdfFileInfo> Files = new List<PdfFileInfo>();

		public MainWindow()
		{
			InitializeComponent();

			folderBrowser.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			textBoxPath.Text = folderBrowser.SelectedPath;

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

		private void Filter()
		{
			string pattern = textBoxFilter.Text.ToLower();

			List<Regex> filters = new List<Regex>();

			foreach (Match filter in patternParser.Matches(pattern))
			{
				filters.Add(new Regex(Regex.Escape(filter.Value.Trim('"')).Replace("\\?", ".?").Replace("\\*", ".*")));
			}

			listView.Items.Clear();

			foreach (PdfFileInfo info in Files)
			{
				if (!filters.Any())
				{
					listView.Items.Add(info);
					continue;
				}

				bool ok = true;

				foreach(Regex filter in filters)
				{
					if (!filter.IsMatch(info.Content))
					{
						ok = false;
						break;
					}
				}

				if(ok)
					listView.Items.Add(info);
			}

			statusMatched.Content = listView.Items.Count.ToString();
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
	}
}
