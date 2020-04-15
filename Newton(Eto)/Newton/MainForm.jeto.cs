using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using RayCasting;
using System.Threading;
using System.Threading.Tasks;
using Eto;
using System.Diagnostics;
using NewtonEngine;

namespace Newton {
    public class MainForm : Form {
        protected Drawable Canvas;
        private readonly Common cmm;

        public MainForm() {
            JsonReader.Load(this);
            cmm = new Common(this, Canvas);
        }
    }
}
