using AnkiSniffer.BLL;
using AnkiSniffer.DataSource;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Xps;

namespace AnkiSniffer {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private List<Card> _cards;

		private List<List<Card>> _selectedCellsHistory;
		private bool _isInBackSel = false;

		private string _fromLang = "eng";
		private string _toLang = "hun";

		private enum ReloadModes {
			Normal,
			Turned,
			FilteredBySztaki
		}

		#region PackagePath property
		public string PackagePath
		{
			get { return (string)GetValue (PackagePathProperty); }
			set { SetValue (PackagePathProperty, value); }
		}

		// Using a DependencyProperty as the backing store for PackagePath.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty PackagePathProperty =
			DependencyProperty.Register ("PackagePath", typeof (string), typeof (MainWindow), new PropertyMetadata (null));
		#endregion

		#region Progress property
		public double Progress
		{
			get { return (double)GetValue (ProgressProperty); }
			set { SetValue (ProgressProperty, value); }
		}

		// Using a DependencyProperty as the backing store for Progress.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ProgressProperty =
			DependencyProperty.Register ("Progress", typeof (double), typeof (MainWindow), new PropertyMetadata (0.0));
		#endregion

		#region WordCount property
		public int WordCount
		{
			get { return (int)GetValue (WordCountProperty); }
			set { SetValue (WordCountProperty, value); }
		}

		// Using a DependencyProperty as the backing store for WordCount.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty WordCountProperty =
			DependencyProperty.Register ("WordCount", typeof (int), typeof (MainWindow), new PropertyMetadata (0));
		#endregion

		#region FilteredWordCount property
		public int FilteredWordCount
		{
			get { return (int)GetValue (FilteredWordCountProperty); }
			set { SetValue (FilteredWordCountProperty, value); }
		}

		// Using a DependencyProperty as the backing store for FilteredWordCount.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty FilteredWordCountProperty =
			DependencyProperty.Register ("FilteredWordCount", typeof (int), typeof (MainWindow), new PropertyMetadata (0)); 
		#endregion

		public MainWindow () {
			InitializeComponent ();

			this.DataContext = this;

			string assDir = System.IO.Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
			PackagePath = System.IO.Path.Combine (assDir, "Erwin_Tschirner_Angol_szkincs__Best_of_English_-_en-hu.apkg");
		}

		private void Reload (ReloadModes mode) {
			List<Card> conversionInput = mode != ReloadModes.Normal ? (List<Card>)this.dgList.ItemsSource : null;
			if (mode == ReloadModes.FilteredBySztaki && _selectedCellsHistory != null && _selectedCellsHistory.Count > 0) {
				conversionInput = _selectedCellsHistory[_selectedCellsHistory.Count - 1];
			}

			this.btnOpen.IsEnabled = false;
			this.btnPrint.IsEnabled = false;
			this.btnTurnLangs.IsEnabled = false;
			this.btnCheckSztaki.IsEnabled = false;
			this.dgList.IsEnabled = false;

			_selectedCellsHistory = new List<List<Card>> ();
			this.btnBackSel.IsEnabled = false;

			_cards = null;
			this.dgList.ItemsSource = null;

			string packagePath = PackagePath;
			Task.Run (() => {
				switch (mode) {
					case ReloadModes.FilteredBySztaki:
						_cards = AnkiDataSource.FilterSztaki (conversionInput, _toLang, SztakiProgressHandler);
						break;
					case ReloadModes.Turned:
						_cards = AnkiDataSource.TurnLanguages (conversionInput);
						break;
					default:
					case ReloadModes.Normal:
						_cards = AnkiDataSource.LoadPackage (packagePath, new FastZipEvents () {
							Progress = this.ZipProgressHandler,
							ProgressInterval = TimeSpan.FromMilliseconds (100)
						});
						break;
				}

				//Fill UI
				Dispatcher.Invoke (() => {
					Progress = 100;
					this.btnOpen.IsEnabled = true;
					this.btnPrint.IsEnabled = true;
					this.btnTurnLangs.IsEnabled = mode == ReloadModes.Normal || mode == ReloadModes.FilteredBySztaki;
					this.btnCheckSztaki.IsEnabled = mode == ReloadModes.Normal || mode == ReloadModes.Turned;
					this.dgList.IsEnabled = true;
					this.dgList.ItemsSource = _cards;

					switch (mode) {
						case ReloadModes.Turned:
							this.dgList.Columns[0].Header = "Magyar szó";
							this.dgList.Columns[1].Header = "Angol jelentés";

							this.lbEnglishSearch.Content = "Magyar szó:";
							this.lbHungarySearch.Content = "Angol szó:";

							_fromLang = "hun";
							_toLang = "eng";
							break;
						case ReloadModes.Normal:
							this.dgList.Columns[0].Header = "Angol szó";
							this.dgList.Columns[1].Header = "Magyar jelentés";

							this.lbEnglishSearch.Content = "Angol szó:";
							this.lbHungarySearch.Content = "Magyar szó:";

							_fromLang = "eng";
							_toLang = "hun";
							break;
						default:
							break;
					}

					WordCount = _cards.Count;
					FilteredWordCount = _cards.Count;
				});
			});
		}

		private int GetRowIndexFromCellInfo (DataGridCellInfo dataGridCellInfo) {
			DataGridRow dgrow = (DataGridRow)this.dgList.ItemContainerGenerator.ContainerFromItem (dataGridCellInfo.Item);
			if (dgrow != null)
				return dgrow.GetIndex ();
			return -1;
		}

		private void btnOpen_Click (object sender, RoutedEventArgs e) {
			Reload (ReloadModes.Normal);
		}

		private void ZipProgressHandler (object sender, ProgressEventArgs e) {
			Progress = e.PercentComplete * 100.0;
		}

		private void SztakiProgressHandler (double percent) {
			Dispatcher.Invoke (() => {
				Progress = percent;
			});
		}

		private void btnPrint_Click (object sender, RoutedEventArgs e) {
			if (_cards == null)
				return;

			//Compose document
			FlowDocument doc = new FlowDocument () {
				FontSize = 8
			};

			Table table = new Table ();
			table.Columns.Add (new TableColumn () {
				Width = new GridLength (1, GridUnitType.Star)
			});
			table.Columns.Add (new TableColumn () {
				Width = new GridLength (1.5, GridUnitType.Star)
			});
			table.Columns.Add (new TableColumn () {
				Width = new GridLength (4, GridUnitType.Star)
			});

			TableRowGroup rows = new TableRowGroup ();
			table.RowGroups.Add (rows);

			for (int i = 0; i < _cards.Count; ++i) {
				//Add header
				//if (i == 0) {
				//	TableRow rowHeader = new TableRow ();

				//	TableCell cell_1 = new TableCell ();
				//	cell_1.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse ("Angol szó")) {
				//		FontWeight = FontWeights.Bold
				//	});
				//	rowHeader.Cells.Add (cell_1);

				//	TableCell cell_2 = new TableCell ();
				//	cell_2.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse ("Magyar jelentés")) {
				//		FontWeight = FontWeights.Bold
				//	});
				//	rowHeader.Cells.Add (cell_2);

				//	TableCell cell_3 = new TableCell ();
				//	cell_3.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse ("Példa")) {
				//		FontWeight = FontWeights.Bold
				//	});
				//	rowHeader.Cells.Add (cell_3);

				//	rows.Rows.Add (rowHeader);
				//}

				//Add content
				TableRow row = new TableRow ();

				TableCell cell_content_1 = new TableCell ();
				cell_content_1.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse (_cards[i].Word)));
				row.Cells.Add (cell_content_1);

				TableCell cell_content_2 = new TableCell ();
				cell_content_2.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse (_cards[i].FlatTranslate)));
				row.Cells.Add (cell_content_2);

				TableCell cell_content_3 = new TableCell ();
				cell_content_3.Blocks.Add (new Paragraph (FormatterBehaviour.Traverse (_cards[i].FlatExamples)));
				row.Cells.Add (cell_content_3);

				rows.Rows.Add (row);
			}

			doc.Blocks.Add (table);

			//Print document
			PrintDocumentImageableArea ia = null;
			XpsDocumentWriter docWriter = PrintQueue.CreateXpsDocumentWriter (ref ia);

			if (docWriter != null && ia != null) {
				DocumentPaginator paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;

				// Change the PageSize and PagePadding for the document to match the CanvasSize for the printer device.
				paginator.PageSize = new Size (ia.MediaSizeWidth, ia.MediaSizeHeight);
				Thickness t = new Thickness (72);  // copy.PagePadding;
				doc.PagePadding = new Thickness (
					Math.Max (ia.OriginWidth, t.Left),
					Math.Max (ia.OriginHeight, t.Top),
					Math.Max (ia.MediaSizeWidth - (ia.OriginWidth + ia.ExtentWidth), t.Right),
					Math.Max (ia.MediaSizeHeight - (ia.OriginHeight + ia.ExtentHeight), t.Bottom));

				doc.ColumnWidth = double.PositiveInfinity;
				//doc.PageWidth = 528; // allow the page to be the natural with of the output device

				// Send content to the printer.
				docWriter.Write (paginator);
			}
		}

		private void tbEnglishSearch_TextChanged (object sender, TextChangedEventArgs e) {
			string currentFilter = this.tbEnglishSearch.Text;
			if (string.IsNullOrEmpty (currentFilter)) {
				this.dgList.ItemsSource = _cards;
				FilteredWordCount = _cards.Count;
			} else {
				List<Card> filteredCards = (from item in _cards
											where item.Word.ToLower ().Contains (currentFilter.ToLower ())
											select item).ToList ();
				this.dgList.ItemsSource = filteredCards;
				FilteredWordCount = filteredCards.Count;
			}
		}

		private void tbHungarySearch_TextChanged (object sender, TextChangedEventArgs e) {
			string currentFilter = this.tbHungarySearch.Text;
			if (string.IsNullOrEmpty (currentFilter)) {
				this.dgList.ItemsSource = _cards;
				FilteredWordCount = _cards.Count;
			} else {
				List<Card> filteredCards = (from item in _cards
											where string.Join ("", item.Translate).ToLower ().Contains (currentFilter.ToLower ())
											select item).ToList ();
				this.dgList.ItemsSource = filteredCards;
				FilteredWordCount = filteredCards.Count;
			}
		}

		private void btnBackSel_Click (object sender, RoutedEventArgs e) {
			_isInBackSel = true;

			if (_selectedCellsHistory.Count > 0) {
				this.tbEnglishSearch.Text = string.Empty;
				this.tbHungarySearch.Text = string.Empty;

				this.dgList.ItemsSource = _cards;
				FilteredWordCount = _cards.Count;

				List<Card> selItems = _selectedCellsHistory[_selectedCellsHistory.Count - 1];
				_selectedCellsHistory.RemoveAt (_selectedCellsHistory.Count - 1);

				this.dgList.SelectedItems.Clear ();
				foreach (Card card in selItems)
					this.dgList.SelectedItems.Add (card);
			}

			this.btnBackSel.IsEnabled = _selectedCellsHistory.Count > 0;

			_isInBackSel = false;
		}

		private void dgList_SelectionChanged (object sender, SelectionChangedEventArgs e) {
			if (_isInBackSel)
				return;

			var selItems = this.dgList.SelectedItems;
			if (selItems != null && selItems.Count > 0) {
				List<Card> selCards = new List<Card> ();
				foreach (Card card in selItems)
					selCards.Add (card);
				_selectedCellsHistory.Add (selCards);
			}

			this.btnBackSel.IsEnabled = _selectedCellsHistory.Count > 0;
		}

		private void btnTurnLangs_Click (object sender, RoutedEventArgs e) {
			Reload (ReloadModes.Turned);
		}

		private void btnCheckSztaki_Click (object sender, RoutedEventArgs e) {
			Reload (ReloadModes.FilteredBySztaki);
		}
	}
}
