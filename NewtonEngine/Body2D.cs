using RayCasting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
#if WINFORMS
using System.Drawing;
#else
using Eto.Drawing;
#endif
using System.Linq;

namespace Newton {
    public class Body2D : Vector, ICloneable {
        private Brush b;
        private double mMass;
        private double m2;
        private const double e = 0.1; // Epsilon
        private const double minDistance = 1.0;

        private Vector vel; // Linear Velocity
        private Vector acc; // Linear Acceleration
        private Vector lastPos;

        public Vector Velocity {
            get => vel;
            set => vel = value;
        }
        public Vector Acceleration {
            get => acc;
            set => acc = value;
        }
        public new Color Color {
            get => base.Color;
            set {
                base.Color = value;
                b?.Dispose();
                b = new SolidBrush(value);
            }
        }
        public double Mass {
            get => mMass;
            set {
                mMass = value;
                m2 = mMass / 2;
                Magnitude = m2;
            }
        }
        public double Restitution { get; set; } = 0.8; // Coefficient of restitution
        public bool Movable { get; set; } = true;
        public List<PointD> History { get; } = new List<PointD>();
        public int HistorySize { get; set; } = 0;

        public Body2D(double mass, double x, double y) : base(0, 0, x, y) {
            vel = new Vector(0, 0, Origin);
            acc = new Vector(0, 0, Origin);

            Mass = mass;

#if WINFORMS
            Color = Color.Gray;
#else
            Color = Colors.Gray;
#endif
        }

        public Body2D(double mass, double x, double y, Color color) : this(mass, x, y) {
            Color = color;
        }

        public void Update(List<Vector> forces, RectangleF bounds, double deltaTime, bool constrainToBounds = true) {
            if(HistorySize > 0) {
                if(History.Count > HistorySize) History.RemoveAt(0);
                History.Add(Origin);
            }

            if(Movable) {
                if(forces.Count > 0) {
                    acc = forces[0];
                    for(int i = 1; i < forces.Count; i++) acc += forces[i] / mMass;
                }
                vel += acc;
                lastPos = new Vector(this);
                Move(vel * deltaTime);
            }

            if(constrainToBounds) CheckBoundsCollisions(bounds);
        }

        public static void CheckBodiesCollisions(List<Body2D> bodies, List<Vector> forces, double deltaTime) {
            Vector[] newVels = new Vector[bodies.Count];

            for(int i = 0; i < bodies.Count; i++) {
                if(!bodies[i].Movable) continue;

                for(int j = 0; j < bodies.Count; j++) {
                    if(i != j && GetImpactVector(bodies[i], bodies[j]).Magnitude <= minDistance) {
                        int[] colBodies = GetCollidingBodies(bodies, j, new List<int> { i });
                        Body2D bigBody = colBodies.Length == 1 ?
                                            bodies[j] :
                                            GetBodiesSum(bodies, colBodies);

                        // Perform the analysis with a body that is one step ahead of the actual body.
                        // This way we can determine if the bodies would collide before they actually do.
                        Body2D testBody = (Body2D)bodies[i].Clone();
                        testBody.Update(forces, RectangleF.Empty, deltaTime, false);

                        if(newVels[i] == null) {
                            newVels[i] = Run2DCollision(testBody, bigBody).Velocity;
                        } else {
                            Vector v = Run2DCollision(testBody, bigBody).Velocity;
                            if(v != null) newVels[i] -= v;
                        }
                    }
                }
            }

            for(int i = 0; i < newVels.Length; i++)
                if(newVels[i] != null) {
                    bodies[i].vel = newVels[i];
                    bodies[i].TranslateAbs(bodies[i].lastPos); // Reset to position before the collision

                }
        }

        private static Body2D GetBodiesSum(List<Body2D> bodies, int[] colBodies) {
            Body2D newBody = null;
            PointD p = new PointD();

            for(int i = 0; i < colBodies.Length; i++) {
                if(newBody == null) {
                    newBody = (Body2D)(bodies[colBodies[i]].Clone());
                } else {
                    newBody.Mass += bodies[colBodies[i]].Mass;
                    newBody.Velocity += bodies[colBodies[i]].Velocity;
                }
                p.X += bodies[colBodies[i]].X1;
                p.Y += bodies[colBodies[i]].Y1;
            }

            p.X /= colBodies.Length;
            p.Y /= colBodies.Length;
            newBody.Origin = p;

            return newBody;
        }

        private static int[] GetCollidingBodies(List<Body2D> bodies, int b1, List<int> ignore) {
            List<int> bs = new List<int> { b1 };
            List<int> il = new List<int>();

            il.AddRange(ignore.ToArray());

            for(int b2 = 0; b2 < bodies.Count; b2++) {
                if(b2 != b1 &&
                    !il.Contains(b2) &&
                    GetImpactVector(bodies[b1], bodies[b2]).Magnitude <= minDistance) {
                    il.AddRange(new int[] { b1, b2 });
                    bs.AddRange(GetCollidingBodies(bodies, b2, il));
                }
            }

            return bs.Distinct().ToArray();
        }

        public static Vector GetImpactVector(Body2D b1, Body2D b2) {
            Vector iv = new Vector(b1.Origin, b2.Origin);
            iv.Magnitude = Math.Abs(iv.Magnitude) - (b1.m2 + b2.m2);
            return iv;
        }

        public static (Vector Velocity, Vector ImpactVector) Run2DCollision(Body2D b1, Body2D b2) {
            Vector iv = GetImpactVector(b1, b2);
            if(iv.Magnitude <= minDistance) {
                // 2D Collision (http://www.sciencecalculators.org/mechanics/collisions/)
                double phi = iv.Angle;
                double m1 = b1.Mass * b1.Restitution;
                double m2 = b2.Mass * b2.Restitution;
                double a1 = b1.vel.Angle;
                double a2 = b2.vel.Angle;
                double v1 = b1.vel.Magnitude;
                double v2 = b2.vel.Magnitude;

                double t1 = v1 * Math.Cos(a1 - phi) * (m1 - m2);
                double t2 = 2 * m2 * v2 * Math.Cos(a2 - phi);
                double t3x = v1 * Math.Sin(a1 - phi) * Math.Cos(phi + PI90);
                double t3y = v1 * Math.Sin(a1 - phi) * Math.Sin(phi + PI90);
                double f = (t1 + t2) / (m1 + m2);

                double vx = f * Math.Cos(phi) + t3x;
                double vy = f * Math.Sin(phi) + t3y;

                return (new Vector(PointD.Empty, new PointD(vx, vy)), iv);

                // 1D Collision
                //newVels[i] = (objects[i].Velocity * (objects[i].Mass - objects[j].Mass) +
                //            2 * objects[j].Mass * objects[j].Velocity) /
                //            (objects[i].Mass + objects[j].Mass);
            }

            return (null, new Vector());
        }

        private void CheckBoundsCollisions(RectangleF bounds) {
            if(X1 - m2 + e < bounds.X) { // Left Bound
                TranslateAbs(bounds.X + m2, Y1);
                vel.Angle = 0 + PI180 - vel.Angle;
                vel.Magnitude *= Restitution;
            } else if(X1 + m2 - e > bounds.Right) { // Right Bound
                TranslateAbs(bounds.Right - m2, Y1);
                vel.Angle = PI180 + 0 - vel.Angle;
                vel.Magnitude *= Restitution;
            }

            if(Y1 + m2 - e > bounds.Bottom) { // Top Bound
                TranslateAbs(X1, bounds.Bottom - m2);
                vel.Angle = PI270 + PI90 - vel.Angle;
                vel.Magnitude *= Restitution;
            } else if(Y1 - m2 + e < bounds.Y) { // Bottom Bound
                TranslateAbs(X1, bounds.Y + m2);
                vel.Angle = PI90 + PI270 - vel.Angle;
                vel.Magnitude *= Restitution;
            }
        }

        public void Render(Graphics g) {
            RectangleF r = new RectangleF((float)(Origin.X - m2),
                                          (float)(Origin.Y - m2),
                                          (float)Mass,
                                          (float)Mass);
            g.FillEllipse(b, r);
        }

        public bool Intersects(PointD p) {
            double dx = p.X - X1;
            double dy = p.Y - Y1;
            double m = m2 * m2;
            return ((dx * dx) + (dy * dy)) / m < 1.0;
        }

        public object Clone() {
            Body2D b = new Body2D(mMass, X1, Y1) {
                Color = this.Color,
                Movable = this.Movable,
                Restitution = this.Restitution,
                Tag = this.Tag,
                Velocity = new Vector(this.Velocity),
                HistorySize = this.HistorySize
            };
            b.Reset(this);
            this.History.ForEach((p) => b.History.Add(p));

            return b;
        }
    }
}