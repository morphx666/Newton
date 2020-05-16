using Eto.Forms;
using Eto.Serialization.Json;
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
