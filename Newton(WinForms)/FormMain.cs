using NewtonEngine;
using RayCasting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
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