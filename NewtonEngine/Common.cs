using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if WINFORMS
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing;
#else
using Eto.Drawing;
using Eto.Forms;
#endif
using Newton;
using RayCasting;

namespace NewtonEngine {
    public class Common {
        private readonly List<Body2D> bodies = new List<Body2D>();

        private enum Modes {
            Standard,
            Planetarium
        }

        private readonly Vector gravity = new Vector(0, -9.8);
        private readonly Vector wind = new Vector(15.0, 0);
        private readonly List<Vector> forces = new List<Vector>();

        private RectangleF bounds;
        private Body2D overBody;
        private bool overBodyCanMove;
        private bool isDragging;
        private Modes mode = Modes.Standard;

        private double scale = 1.0;
        private double simSpeed = 1.0;
#if WINFORMS
        private readonly Multimedia.Timer tmr;
#endif
        private readonly AutoResetEvent evt = new AutoResetEvent(false);
        private readonly double dtFactor = 1000.0 * TimeSpan.TicksPerMillisecond;
        private int fps = 90;
        private int fpsDelay;

        private bool isClosing = false;

#if WINFORMS
        private readonly Form parent;
        private readonly Form canvas;
#else
        private Form parent;
        private Drawable canvas;
#endif

        private bool showHelp = true;
        private readonly Font helpFont;
        private const string helpInfo = "[ENTER] = Restart Simulation\n" +
                                       "[SPACE] = Switch Simulation Mode\n" +
                                       "[<] [>] = Change Simulation Speed\n" +
                                       "[+] [-] = Change Scale/Zoom\n" +
                                       "[F1]    = Toggle this information";

        public Common(object parentForm, object etoSurface) {
#if WINFORMS
            parent = (FormMain)parentForm;
            canvas = parent;
#else
            parent = (MainForm)parentForm;
            canvas = (Drawable)etoSurface;
#endif

            fpsDelay = (int)(dtFactor / fps);

#if WINFORMS
            // So far, this is the only way to get sub 15ms resolutions
            // while VS is not running
            // https://www.codeproject.com/Articles/5501/The-Multimedia-Timer-for-the-NET-Framework
            tmr = new Multimedia.Timer {
                Mode = Multimedia.TimerMode.Periodic,
                Period = 5,
                Resolution = 1
            };
            tmr.Tick += (_, __) => evt.Set();

            UpdateTitlebarText();
            SetBounds();
            CreateRandomObjects();
            SetEventsHandlers();

            RunSimLoop();
#else
            parent.Shown += (_, __) => {
                UpdateTitlebarText();
                SetBounds();
                SetEventsHandlers();
                CreateRandomObjects();

                RunSimLoop();
            };
#endif

            helpFont = GetMonoFont();
        }

        private Font GetMonoFont() {
#if WINFORMS
            return new Font("Consolas", 12);
#else
            foreach(FontFamily ff in Fonts.AvailableFontFamilies) {
                if(ff.Name.ToLower().Contains("consolas") ||
                   ff.Name.ToLower().Contains("monospace") ||
                   ff.Name.ToLower().Contains("console"))
                    return new Font(ff, 12);
            }
            return Fonts.Monospace(12);
#endif
        }

        private void RunSimLoop() {
            Task.Run(() => {
                long ct;
                long lt = DateTime.Now.Ticks;
                double dt = 0;
                double c = 0;

                while(!isClosing) {
#if WINFORMS
                    evt.WaitOne();
#else
                    evt.WaitOne(5);
#endif

                    ct = DateTime.Now.Ticks;
                    dt = (ct - lt);
                    lt = ct;
                    c += dt;

                    dt /= dtFactor;
                    dt *= simSpeed;

                    if(c >= fpsDelay) {
                        c = 0;
#if WINFORMS
                        parent.Invalidate();
#else
                        Application.Instance.Invoke(() => parent.Invalidate());
#endif
                    }

                    switch(mode) {
                        case Modes.Standard:
                            SimStandard(dt);
                            break;
                        case Modes.Planetarium:
                            SimPlanetarium(dt);
                            break;
                    }

                    Body2D.CheckBodiesCollisions(bodies, forces, dt);
                }
            });
        }

        private void CreateRandomObjects() {
#if WINFORMS
            tmr.Stop();
#endif
            Thread.Sleep(15); // Give the SimLoop enough time to finish

            bodies.Clear();
            forces.Clear();

            switch(mode) {
                case Modes.Standard:
                    Random rnd = new Random();
                    int n = rnd.Next(3, (int)Math.Sqrt(bounds.Width));
                    double mass;
                    double x;
                    double y;

                    for(int i = 0; i < n; i++) {
                        while(true) {
                            bool isValid = true;

                            mass = rnd.NextDouble() * 100 + 20;
                            x = rnd.NextDouble() * bounds.Width - bounds.Width / 2;
                            y = rnd.NextDouble() * bounds.Height - bounds.Height / 2;

                            RectangleF r = new RectangleF((float)(x - mass / 2) - 5,
                                                          (float)(y - mass / 2) - 5,
                                                          (float)mass + 10,
                                                          (float)mass + 10);

                            if(x - mass / 2 < bounds.X || y - mass / 2 < bounds.Y ||
                                x + mass > bounds.Width / 2 || y + mass > bounds.Height) {
                                isValid = false;
                            } else {
                                for(int j = 0; j < bodies.Count; j++) {
                                    double m = bodies[j].Mass;
                                    RectangleF rp = new RectangleF((float)(bodies[j].X1 - m / 2),
                                                                   (float)(bodies[j].Y1 - m / 2),
                                                                   (float)m,
                                                                   (float)m);
                                    rp.Inflate(5, 5);
#if WINFORMS
                                    if(rp.IntersectsWith(r)) {
#else
                                    if(rp.Intersects(r)) {
#endif
                                        isValid = false;
                                        break;
                                    }
                                }
                            }
                            if(isValid) break;
                        }

                        bodies.Add(new Body2D(mass, x, y) {
                            Color = Color.FromArgb((int)(rnd.NextDouble() * 255),
                                                   (int)(rnd.NextDouble() * 255),
                                                   (int)(rnd.NextDouble() * 255))
                        });
                    }

                    forces.Add(gravity);
                    forces.Add(wind);

                    break;
                case Modes.Planetarium:
#if WINFORMS
                    bodies.Add(new Body2D(200, 0, 0) { Color = Color.Yellow, Movable = false });

                    bodies.Add(new Body2D(30, 700, 0) { Color = Color.DeepSkyBlue, HistorySize = 1000 });
                    bodies[1].Velocity = new Vector(200, 90 * Vector.ToRad, bodies[1].Origin);

                    bodies.Add(new Body2D(20, 0, -500) { Color = Color.OrangeRed, HistorySize = 1000 });
                    bodies[2].Velocity = new Vector(200, 0 * Vector.ToRad, bodies[2].Origin);

                    bodies.Add(new Body2D(40, -500, 0) { Color = Color.YellowGreen, HistorySize = 1000 });
                    bodies[3].Velocity = new Vector(220, 290 * Vector.ToRad, bodies[3].Origin);
#else
                    bodies.Add(new Body2D(200, 0, 0) { Color = Colors.Yellow, Movable = false });

                    bodies.Add(new Body2D(30, 700, 0) { Color = Colors.DeepSkyBlue, HistorySize = 1000 });
                    bodies[1].Velocity = new Vector(200, 90 * Vector.ToRad, bodies[1].Origin);

                    bodies.Add(new Body2D(20, 0, -500) { Color = Colors.OrangeRed, HistorySize = 1000 });
                    bodies[2].Velocity = new Vector(200, 0 * Vector.ToRad, bodies[2].Origin);

                    bodies.Add(new Body2D(40, -500, 0) { Color = Colors.YellowGreen, HistorySize = 1000 });
                    bodies[3].Velocity = new Vector(220, 290 * Vector.ToRad, bodies[3].Origin);
#endif
                    break;
            }

#if WINFORMS
            tmr.Start();
#endif
        }

        private void SimStandard(double dt) {
            bodies.ForEach((o) => o.Update(forces, bounds, dt));
        }

        private void SimPlanetarium(double dt) {
            double k = 16666.0 * dt;

            for(int i = 0; i < bodies.Count; i++) {
                bodies[i].Restitution = 0.000001;

                forces.Clear();
                forces.Add(new Vector(0, 0)); // Dummy force 

                for(int j = 0; j < bodies.Count; j++) {
                    if(i != j) {
                        Vector v = new Vector(bodies[i].Origin,
                                              bodies[j].Origin);
                        v.Magnitude = k * (bodies[j].Mass * bodies[j].Mass) / (v.Magnitude * v.Magnitude);
                        forces.Add(v);
                    }
                }
                bodies[i].Update(forces, bounds, dt, false);
            };
        }

        private void SetEventsHandlers() {
            canvas.Paint += (object s, PaintEventArgs e) => Paint(e);
            parent.Closing += (_, __) => isClosing = true;
            parent.SizeChanged += (_, __) => SetBounds();
            parent.MouseDown += (_, __) => {
                if(overBody != null) {
                    isDragging = true;
                    overBodyCanMove = overBody.Movable;
                    overBody.Movable = false;
                } else
                    isDragging = false;
            };
            parent.MouseUp += (_, __) => {
                isDragging = false;
                if(overBody != null) overBody.Movable = overBodyCanMove;
            };
            parent.MouseMove += (object o, MouseEventArgs e) => {
                PointD p = e.Location;
                p.X = e.Location.X / scale - bounds.Width / 2;
                p.Y = bounds.Height / 2 - e.Location.Y / scale;

                if(isDragging) {
                    overBody.TranslateAbs(p.X, p.Y);
                } else {
                    Cursor c = Cursors.Default;
                    bodies.ForEach((b) => {
                        if(b.Intersects(p)) {
#if WINFORMS
                            c = Cursors.Hand;
#else
                            c = Cursors.Pointer;
#endif
                            overBody = b;
                            return;
                        }
                    });
                    parent.Cursor = c;
                }
            };
            parent.KeyUp += (object s, KeyEventArgs e) => {
#if WINFORMS
                switch(e.KeyCode) {
#else
                switch(e.Key) {
#endif
                    case Keys.Add:
                        if(scale < 2) {
                            scale += 0.1f;
                            SetBounds();
                            UpdateTitlebarText();
                        }
                        break;
                    case Keys.Subtract:
                        if(scale > 0.1f) {
                            scale -= 0.1f;
                            SetBounds();
                            UpdateTitlebarText();
                        }
                        break;
                    case Keys.Enter:
                        CreateRandomObjects();
                        break;
                    case Keys.Space:
                        if(mode == Modes.Standard)
                            mode = Modes.Planetarium;
                        else
                            mode = Modes.Standard;
                        CreateRandomObjects();
                        break;
#if WINFORMS
                    case Keys.OemPeriod:
#else
                    case Keys.Period:
#endif
                        if(simSpeed < 10) simSpeed += 0.1f;
                        UpdateTitlebarText();
                        break;
#if WINFORMS
                    case Keys.Oemcomma:
#else
                    case Keys.Comma:
#endif
                        if(simSpeed > 0.1) simSpeed -= 0.1f;
                        UpdateTitlebarText();
                        break;
                    case Keys.F1:
                        showHelp = !showHelp;
                        break;
                }
            };
        }

        private void SetBounds() {
#if WINFORMS
            bounds = new RectangleF(-parent.DisplayRectangle.Width / 2,
                                    -parent.DisplayRectangle.Height / 2,
                                     parent.DisplayRectangle.Width,
                                     parent.DisplayRectangle.Height);
#else
            bounds = new RectangleF(-parent.ClientSize.Width / 2,
                                    -parent.ClientSize.Height / 2,
                                     parent.ClientSize.Width,
                                     parent.ClientSize.Height);
#endif
            double s = (1 - scale) * (0.5 / scale);
            bounds.Inflate((float)(bounds.Width * s),
                           (float)(bounds.Height * s));
        }

        private void Paint(PaintEventArgs e) {
            Graphics g = e.Graphics;

#if WINFORMS
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TranslateTransform(parent.DisplayRectangle.Width / 2,
                                 parent.DisplayRectangle.Height / 2);
#else
            g.AntiAlias = false;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.SaveTransform();
            g.TranslateTransform(parent.ClientSize.Width / 2,
                                 parent.ClientSize.Height / 2);
#endif
            g.ScaleTransform((float)scale, -(float)scale);

            bodies.ForEach((o) => {
                if(o.History.Count > 1)
#if WINFORMS
                    using(Pen p = new Pen(Color.FromArgb(96, o.Color), 2))
                        g.DrawCurve(p, Array.ConvertAll(o.History.ToArray(), i => (PointF)i));
#else
                    using(Pen p = new Pen(Color.FromArgb((int)(255.0f * o.Color.R),
                                                         (int)(255.0f * o.Color.G),
                                                         (int)(255.0f * o.Color.B),
                                                         96), 2))
                        g.DrawLines(p, Array.ConvertAll(o.History.ToArray(), i => (PointF)i));
#endif
                o.Render(g);
            });

            RenderHelp(g);
        }

        private void UpdateTitlebarText() {
            string title = $"Newton Simulator: {mode} | Simulation {simSpeed:N1} | Scale {scale:N1}"; ;
#if WINFORMS
            parent.Text = title;
#else
            parent.Title = title;
#endif
        }

        public void RenderHelp(Graphics g) {
            if(showHelp) {
#if WINFORMS
                g.ResetTransform();
                g.DrawString(helpInfo, helpFont, Brushes.White, 5, 5);
#else
                g.RestoreTransform();
                g.DrawText(helpFont, Colors.White, 5, 5, helpInfo);
#endif
            }
        }
    }
}