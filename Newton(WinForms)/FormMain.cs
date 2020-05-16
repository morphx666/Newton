using NewtonEngine;
using System.Drawing;
using System.Windows.Forms;

namespace Newton {
    public partial class FormMain : Form {
        private readonly Common cmm;

        public FormMain() {
            InitializeComponent();

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.Black;

            cmm = new Common(this, null);
        }
    }
}