using System.Collections.Generic;
using System.Windows;

namespace TableCloth
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        public IEnumerable<string> Arguments { get; set; } = new string[0];
    }
}
