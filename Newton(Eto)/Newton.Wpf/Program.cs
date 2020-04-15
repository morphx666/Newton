using System;
using System.Runtime.CompilerServices;
using Eto;
using Eto.Drawing;
using Eto.Forms;

namespace Newton.Wpf {
    class MainClass {
        [STAThread]
        public static void Main(string[] args) {
            new Application(Eto.Platforms.Wpf).Run(new MainForm());
        }
    }
}
