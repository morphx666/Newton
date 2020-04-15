using System;
using System.Collections.Generic;
using Eto.Forms;
using Eto.Drawing;
using Eto.Serialization.Json;
using RayCasting;
using System.Threading;
using System.Threading.Tasks;

namespace Newton {
    public class MainForm : Form {
        protected Drawable Canvas;
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
        private readonly AutoResetEvent evt = new AutoResetEvent(false);
        private readonly double dtFactor = 1000.0 * TimeSpan.TicksPerMillisecond;
        private int fps = 90;
        private int fpsDelay;

        private bool isClosing = false;

        public MainForm() {
            JsonReader.Load(this);

            fpsDelay = (int)(dtFactor / fps);

            this.Shown += (_, __) => {
                UpdateTitlebarText();
                SetBounds();
                SetEventsHandlers();
                CreateRandomObjects();

                RunSimLoop();
            };
        }

        private void RunSimLoop() {
            Task.Run(() => {
                long ct;
                long lt = DateTime.Now.Ticks;
                double dt = 0;
                double c = 0;

                while(!isClosing) {
                    evt.WaitOne(5);

                    ct = DateTime.Now.Ticks;
                    dt = (ct - lt);
                    lt = ct;
                    c += dt;

                    dt /= dtFactor;
                    dt *= simSpeed;

                    if(c >= fpsDelay) {
                        c = 0;
                        Application.Instance.Invoke(() => this.Invalidate());
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
            Thread.Sleep(15); // Give the SimLoop enough time to finish

            bodies.Clear();
            forces.Clear();

            switch(mode) {
                case Modes.Standard:
                    Random rnd = new Random();
                    int n = rnd.Next(3, 25);
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
                                    if(rp.Intersects(r)) {
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
                    bodies.Add(new Body2D(200, 0, 0) { Color = Colors.Yellow, Movable = false });

                    bodies.Add(new Body2D(30, 700, 0) { Color = Colors.DeepSkyBlue, HistorySize = 1000 });
                    bodies[1].Velocity = new Vector(200, 90 * Vector.ToRad, bodies[1].Origin);

                    bodies.Add(new Body2D(20, 0, -500) { Color = Colors.OrangeRed, HistorySize = 1000 });
                    bodies[2].Velocity = new Vector(200, 0 * Vector.ToRad, bodies[2].Origin);

                    bodies.Add(new Body2D(40, -500, 0) { Color = Colors.YellowGreen, HistorySize = 1000 });
                    bodies[3].Velocity = new Vector(220, 290 * Vector.ToRad, bodies[3].Origin);
                    break;
            }
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

        private void Paint(PaintEventArgs e) {
            Graphics g = e.Graphics;

            g.AntiAlias = false;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            g.TranslateTransform(this.ClientSize.Width / 2,
                                 this.ClientSize.Height / 2);
            g.ScaleTransform((float)scale, -(float)scale);

            bodies.ForEach((o) => {
                if(o.History.Count > 1)
                    using(Pen p = new Pen(Color.FromArgb((int)o.Color.R, (int)o.Color.G, (int)o.Color.B, 128), 2))
                        g.DrawLines(p, Array.ConvertAll(o.History.ToArray(), i => (PointF)i));
                o.Render(g);
            });
        }

        private void SetEventsHandlers() {
            Canvas.Paint += (object s, PaintEventArgs e) => Paint(e);

            this.Closing += (_, __) => isClosing = true;
            this.SizeChanged += (_, __) => SetBounds();
            this.MouseDown += (_, __) => {
                if(overBody != null) {
                    isDragging = true;
                    overBodyCanMove = overBody.Movable;
                    overBody.Movable = false;
                } else
                    isDragging = false;
            };
            this.MouseUp += (_, __) => {
                isDragging = false;
                if(overBody != null) overBody.Movable = overBodyCanMove;
            };
            this.MouseMove += (object o, MouseEventArgs e) => {
                PointD p = e.Location;
                p.X = e.Location.X / scale - bounds.Width / 2;
                p.Y = bounds.Height / 2 - e.Location.Y / scale;

                if(isDragging) {
                    overBody.TranslateAbs(p.X, p.Y);
                } else {
                    Cursor c = Cursors.Default;
                    bodies.ForEach((b) => {
                        if(b.Intersects(p)) {
                            c = Cursors.Pointer;
                            overBody = b;
                            return;
                        }
                    });
                    Cursor = c;
                }
            };

            this.KeyDown += (object s, KeyEventArgs e) => {
                switch(e.Key) {
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
                        UpdateTitlebarText();
                        break;
                    case Keys.Period:
                        if(simSpeed < 10) simSpeed += 0.1f;
                        UpdateTitlebarText();
                        break;
                    case Keys.Comma:
                        if(simSpeed > 0.1) simSpeed -= 0.1f;
                        UpdateTitlebarText();
                        break;
                }
            };
        }

        private void SetBounds() {
            bounds = new RectangleF(-this.ClientSize.Width / 2,
                                    -this.ClientSize.Height / 2,
                                     this.ClientSize.Width,
                                     this.ClientSize.Height);
            double s = (1 - scale) * (0.5 / scale);
            bounds.Inflate((float)(bounds.Width * s),
                           (float)(bounds.Height * s));
        }

        private void UpdateTitlebarText() {
            this.Title = $"Newton Simulator: {mode} | Simulation {simSpeed:N1} | Scale {scale:N1}";
        }
    }
}