using System;
using System.Collections.Generic;
using System.Linq;
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

namespace AnkiSniffer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
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

        public MainWindow () {
            InitializeComponent ();

            this.DataContext = this;

            PackagePath = @"d:\work\Erwin_Tschirner_Angol_szkincs__Best_of_English_-_en-hu.apkg";

            Reload ();
        }

        private void Reload () {
        }
    }
}
