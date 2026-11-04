using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;
using TransporteUrbano.Servicios;

namespace TransporteUrbano.UI
{
    /// <summary>
    /// Mapa de la red dibujado con GDI+ (sin internet ni librerías externas). Las paradas se ubican con su
    /// latitud/longitud, los tramos se trazan con ángulos de 45 grados como en un plano de metro, cada zona
    /// conectada (Union-Find) tiene su color, y la ruta calculada se dibuja animada en azafrán.
    /// Rueda: zoom. Arrastrar: mover. Doble clic: encuadrar todo.
    /// </summary>
    public class MapaControl : Control, IMapaRed
    {
        private SistemaTransporte sistema;

        private Parada[] paradas = new Parada[0];
        private Ruta[] rutas = new Ruta[0];
        private PointF[] plano = new PointF[0];
        private int[] zonaDe = new int[0];
        private int[] gradoDe = new int[0];
        private int[] conteoZona = new int[1];
        private int totalZonas;
        private readonly TablaHash<int> indice = new TablaHash<int>();        // código -> posición en los arreglos
        private readonly TablaHash<int> ordenCamino = new TablaHash<int>();   // código -> posición en la ruta
        private readonly TablaHash<int> ordenSecuencia = new TablaHash<int>();// código -> orden de visita BFS/DFS

        private ListaEnlazada<string> camino;
        private ListaEnlazada<string> secuencia;
        private float progreso = 1f;
        private readonly Timer animacion = new Timer { Interval = 16 };

        private bool refDefinida;
        private bool refDePais;     // la referencia actual es la del país (mapa sin paradas)
        private double latRef, lonRef, cosLat = 1;
        private double escala = 1, escalaFit = 1, offX, offY;
        private bool vistaLibre;

        private bool arrastrando, huboArrastre;
        private Point ultimo, inicioArrastre;
        private int hover = -1;
        private string seleccionada;

        private readonly Font fEtiqueta = Tema.Fuente(8.5f);
        private readonly Font fNegrita = Tema.Fuente(9.5f, FontStyle.Bold);
        private readonly Font fPin = Tema.Fuente(8.5f, FontStyle.Bold);
        private readonly Font fGrande = Tema.Fuente(17f, FontStyle.Bold);
        private readonly Font fSuave = Tema.Fuente(9.5f);
        private readonly StringFormat sfCentro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        public bool ModoElegirUbicacion { get; set; }
        public event Action<string> ParadaClick;
        public event Action<double, double> UbicacionElegida;

        public MapaControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Tema.ColMapa;
            animacion.Tick += (s, e) =>
            {
                progreso += 0.022f;
                if (progreso >= 1f) { progreso = 1f; animacion.Stop(); }
                Invalidate();
            };
        }

        public Control Vista { get { return this; } }

        public void IniciarElegirUbicacion()
        {
            ModoElegirUbicacion = true;
            Cursor = Cursors.Cross;
            Invalidate();
        }

        public SistemaTransporte Sistema
        {
            get { return sistema; }
            set { sistema = value; Refrescar(); }
        }

        // ------------------------------------------------------------------ datos

        public void Refrescar()
        {
            if (sistema == null) return;
            bool estabaVacio = paradas.Length == 0;

            paradas = sistema.ListarParadas().ToArray();
            rutas = sistema.ListarRutas().ToArray();
            indice.Limpiar();
            for (int i = 0; i < paradas.Length; i++) indice.Insertar(paradas[i].Codigo, i);

            TablaHash<int> grados = sistema.Grados();
            totalZonas = sistema.CantidadZonas;
            zonaDe = new int[paradas.Length];
            gradoDe = new int[paradas.Length];
            conteoZona = new int[totalZonas + 2];
            for (int i = 0; i < paradas.Length; i++)
            {
                zonaDe[i] = sistema.NumeroZona(paradas[i].Codigo);
                gradoDe[i] = grados.Obtener(paradas[i].Codigo);
                if (zonaDe[i] < conteoZona.Length) conteoZona[zonaDe[i]]++;
            }

            if (paradas.Length == 0)
            {
                DefinirReferenciaPais();
                plano = new PointF[0];
                vistaLibre = false;
                EncuadrarPais(false);
            }
            else
            {
                if (refDePais) { refDefinida = false; refDePais = false; }
                CalcularProyeccion();
                if (estabaVacio || !vistaLibre) { vistaLibre = false; Reencuadrar(null); }
            }
            Invalidate();
        }

        private void CalcularProyeccion()
        {
            if (paradas.Length == 0) { plano = new PointF[0]; return; }
            if (!refDefinida)
            {
                double minLat = double.MaxValue, maxLat = double.MinValue, minLon = double.MaxValue, maxLon = double.MinValue;
                foreach (Parada p in paradas)
                {
                    if (p.Latitud < minLat) minLat = p.Latitud;
                    if (p.Latitud > maxLat) maxLat = p.Latitud;
                    if (p.Longitud < minLon) minLon = p.Longitud;
                    if (p.Longitud > maxLon) maxLon = p.Longitud;
                }
                latRef = (minLat + maxLat) / 2;
                lonRef = (minLon + maxLon) / 2;
                cosLat = Math.Cos(latRef * Math.PI / 180);
                refDefinida = true;
            }
            plano = new PointF[paradas.Length];
            for (int i = 0; i < paradas.Length; i++)
                plano[i] = new PointF((float)((paradas[i].Longitud - lonRef) * cosLat), (float)(-(paradas[i].Latitud - latRef)));
        }

        private void DefinirReferenciaPais()
        {
            latRef = 15.7;
            lonRef = -90.35;
            cosLat = Math.Cos(latRef * Math.PI / 180);
            refDefinida = true;
            refDePais = true;
        }

        private PointF PantallaLatLon(double lat, double lon)
        {
            double x = (lon - lonRef) * cosLat, y = -(lat - latRef);
            return new PointF((float)(offX + x * escala), (float)(offY + y * escala));
        }

        /// <summary>Encuadra todo el país de Guatemala (contorno incrustado).</summary>
        public void EncuadrarPais(bool manual)
        {
            if (Width < 50 || Height < 50) return;
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < MapaBase.Contorno.GetLength(0); i++)
            {
                double x = (MapaBase.Contorno[i, 1] - lonRef) * cosLat, y = -(MapaBase.Contorno[i, 0] - latRef);
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            const int margen = 50;
            escala = Math.Min((Width - 2 * margen) / (maxX - minX), (Height - 2 * margen) / (maxY - minY));
            if (refDePais) escalaFit = escala;
            offX = Width / 2.0 - (minX + maxX) / 2.0 * escala;
            offY = Height / 2.0 - (minY + maxY) / 2.0 * escala;
            vistaLibre = manual;
            Invalidate();
        }

        private PointF Pantalla(int i)
        {
            return new PointF((float)(offX + plano[i].X * escala), (float)(offY + plano[i].Y * escala));
        }

        /// <summary>Encuadra todas las paradas, o solo las indicadas.</summary>
        public void Reencuadrar(ListaEnlazada<string> codigos)
        {
            if (paradas.Length == 0 || Width < 50 || Height < 50) return;

            bool alguno = false;
            float minX = 0, maxX = 0, minY = 0, maxY = 0;
            Action<PointF> ampliar = p =>
            {
                if (!alguno) { minX = maxX = p.X; minY = maxY = p.Y; alguno = true; return; }
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            };

            if (codigos != null)
            {
                foreach (string c in codigos)
                {
                    int i;
                    if (indice.Buscar(c, out i)) ampliar(plano[i]);
                }
            }
            else
            {
                for (int i = 0; i < plano.Length; i++) ampliar(plano[i]);
            }
            if (!alguno) return;

            double w = Math.Max(maxX - minX, 0.004);
            double h = Math.Max(maxY - minY, 0.004);
            const int margen = 90;
            double nueva = Math.Min((Width - 2 * margen) / w, (Height - 2 * margen) / h);
            escala = Math.Max(nueva, 1e-6);
            if (codigos == null) escalaFit = escala;
            offX = Width / 2.0 - (minX + maxX) / 2.0 * escala;
            offY = Height / 2.0 - (minY + maxY) / 2.0 * escala;
            Invalidate();
        }

        public void SeleccionarParada(string codigo, bool centrar)
        {
            seleccionada = codigo;
            int i;
            if (centrar && codigo != null && indice.Buscar(codigo, out i))
            {
                offX = Width / 2.0 - plano[i].X * escala;
                offY = Height / 2.0 - plano[i].Y * escala;
                vistaLibre = true;
            }
            Invalidate();
        }

        public void MostrarCamino(ListaEnlazada<string> codigos)
        {
            LimpiarResaltado();
            camino = codigos;
            int k = 1;
            foreach (string c in codigos) ordenCamino.Establecer(c, k++);
            progreso = 0f;
            Reencuadrar(codigos);
            animacion.Start();
        }

        public void MostrarSecuencia(ListaEnlazada<string> codigos)
        {
            LimpiarResaltado();
            secuencia = codigos;
            int k = 1;
            foreach (string c in codigos) ordenSecuencia.Establecer(c, k++);
            Reencuadrar(codigos);
            Invalidate();
        }

        public void LimpiarResaltado()
        {
            animacion.Stop();
            camino = null;
            secuencia = null;
            ordenCamino.Limpiar();
            ordenSecuencia.Limpiar();
            progreso = 1f;
            Invalidate();
        }

        // ------------------------------------------------------------------ geometría de los tramos

        private static float Dist(PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Longitud(PointF[] p)
        {
            float t = 0;
            for (int i = 1; i < p.Length; i++) t += Dist(p[i - 1], p[i]);
            return t;
        }

        /// <summary>Tramo con una sola quebradura a 45 grados, estilo plano de metro.</summary>
        private static PointF[] Octilineal(PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float adx = Math.Abs(dx), ady = Math.Abs(dy);
            if (adx < 2 || ady < 2 || Math.Abs(adx - ady) < 2) return new[] { a, b };
            float sx = Math.Sign(dx), sy = Math.Sign(dy);
            PointF medio = adx > ady
                ? new PointF(a.X + sx * (adx - ady), a.Y)
                : new PointF(a.X, a.Y + sy * (ady - adx));
            return new[] { a, medio, b };
        }

        /// <summary>Polilínea de i a j. Siempre se calcula desde el índice menor para que ida y vuelta coincidan.</summary>
        private static PointF[] Tramo(PointF[] pts, int i, int j)
        {
            if (i < j) return Octilineal(pts[i], pts[j]);
            PointF[] p = Octilineal(pts[j], pts[i]);
            Array.Reverse(p);
            return p;
        }

        private static PointF PuntoEn(PointF[] p, float distancia, out float angulo)
        {
            for (int i = 1; i < p.Length; i++)
            {
                float s = Dist(p[i - 1], p[i]);
                if (distancia <= s || i == p.Length - 1)
                {
                    float t = s <= 0 ? 0 : Math.Min(1f, distancia / s);
                    angulo = (float)Math.Atan2(p[i].Y - p[i - 1].Y, p[i].X - p[i - 1].X);
                    return new PointF(p[i - 1].X + (p[i].X - p[i - 1].X) * t, p[i - 1].Y + (p[i].Y - p[i - 1].Y) * t);
                }
                distancia -= s;
            }
            angulo = 0;
            return p[0];
        }

        private static PointF[] Recortar(PointF[] p, float fraccion)
        {
            float objetivo = Longitud(p) * fraccion;
            var res = new ListaEnlazada<PointF>();
            res.AgregarFinal(p[0]);
            for (int i = 1; i < p.Length; i++)
            {
                float s = Dist(p[i - 1], p[i]);
                if (objetivo >= s) { res.AgregarFinal(p[i]); objetivo -= s; }
                else
                {
                    float t = s <= 0 ? 0 : objetivo / s;
                    res.AgregarFinal(new PointF(p[i - 1].X + (p[i].X - p[i - 1].X) * t, p[i - 1].Y + (p[i].Y - p[i - 1].Y) * t));
                    break;
                }
            }
            return res.ToArray();
        }

        private PointF[] PolilineaCamino(PointF[] pts)
        {
            if (camino == null || camino.Cantidad < 2) return null;
            var acumulado = new ListaEnlazada<PointF>();
            int anterior = -1;
            foreach (string c in camino)
            {
                int i;
                if (!indice.Buscar(c, out i)) return null;
                if (anterior >= 0)
                {
                    PointF[] t = Tramo(pts, anterior, i);
                    for (int k = acumulado.Cantidad == 0 ? 0 : 1; k < t.Length; k++) acumulado.AgregarFinal(t[k]);
                }
                anterior = i;
            }
            return acumulado.Cantidad >= 2 ? acumulado.ToArray() : null;
        }

        // ------------------------------------------------------------------ dibujo

        private RectangleF BotonZoom(int k)
        {
            float arriba = Height - 16 - (4 * 34 + 3 * 4);
            return new RectangleF(Width - 50, arriba + k * 38, 34, 34);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Tema.ColMapa);
            DibujarBase(g);
            DibujarCuadricula(g);

            if (paradas.Length == 0) { DibujarVacio(g); DibujarSuperposiciones(g); return; }

            var pts = new PointF[paradas.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Pantalla(i);

            DibujarTramos(g, pts);
            DibujarCamino(g, pts);
            DibujarParadas(g, pts);
            DibujarEtiquetas(g, pts);
            DibujarTarjetaHover(g, pts);
            DibujarSuperposiciones(g);
        }

        private void DibujarCuadricula(Graphics g)
        {
            const int paso = 56;
            using (var lapiz = new Pen(Color.FromArgb(16, 255, 255, 255), 1f))
            {
                float x0 = (float)(((offX % paso) + paso) % paso);
                float y0 = (float)(((offY % paso) + paso) % paso);
                for (float x = x0; x < Width; x += paso) g.DrawLine(lapiz, x, 0, x, Height);
                for (float y = y0; y < Height; y += paso) g.DrawLine(lapiz, 0, y, Width, y);
            }
        }

        private void DibujarVacio(Graphics g)
        {
            const string t1 = "Aún no hay paradas en la red";
            const string t2 = "Carga paradas.csv o registra una parada desde el panel izquierdo.";
            float w = Math.Max(g.MeasureString(t1, fGrande).Width, g.MeasureString(t2, fSuave).Width) + 48;
            var caja = new RectangleF((Width - w) / 2, Height / 2f - 48, w, 96);
            using (var path = Tema.Redondeado(caja, 10))
            {
                using (var f = new SolidBrush(Color.FromArgb(235, Tema.ColPanel))) g.FillPath(f, path);
                using (var b = new Pen(Tema.ColBorde)) g.DrawPath(b, path);
            }
            using (var b = new SolidBrush(Tema.ColTexto)) g.DrawString(t1, fGrande, b, new RectangleF(caja.X, caja.Y + 6, caja.Width, 50), sfCentro);
            using (var b = new SolidBrush(Tema.ColSuave)) g.DrawString(t2, fSuave, b, new RectangleF(caja.X, caja.Y + 52, caja.Width, 36), sfCentro);
        }

        /// <summary>Contorno de Guatemala y cabeceras de referencia (datos incrustados, sin internet).</summary>
        private void DibujarBase(Graphics g)
        {
            if (escala > 150000) return;   // muy cerca: el país queda fuera de pantalla y GDI+ no necesita dibujarlo
            int n = MapaBase.Contorno.GetLength(0);
            var pts = new PointF[n];
            for (int i = 0; i < n; i++) pts[i] = PantallaLatLon(MapaBase.Contorno[i, 0], MapaBase.Contorno[i, 1]);
            try
            {
                using (var tierra = new SolidBrush(Color.FromArgb(255, 18, 36, 58))) g.FillPolygon(tierra, pts);
                using (var costa = new Pen(Color.FromArgb(120, 143, 165, 192), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPolygon(costa, pts);
            }
            catch (OverflowException) { return; }

            if (escala > 6000) return;   // las cabeceras solo se muestran a escala de país
            for (int i = 0; i < MapaBase.CiudadesNombre.Length; i++)
            {
                PointF c = PantallaLatLon(MapaBase.CiudadesCoord[i, 0], MapaBase.CiudadesCoord[i, 1]);
                using (var b = new SolidBrush(Color.FromArgb(150, 143, 165, 192))) g.FillEllipse(b, c.X - 3, c.Y - 3, 6, 6);
                using (var b = new SolidBrush(Color.FromArgb(150, 143, 165, 192))) g.DrawString(MapaBase.CiudadesNombre[i], fEtiqueta, b, c.X + 7, c.Y - 7);
            }
        }

        private void DibujarTramos(Graphics g, PointF[] pts)
        {
            using (var casco = new Pen(Color.FromArgb(235, Tema.ColMapa), 9f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                foreach (Ruta r in rutas)
                {
                    int io, id;
                    if (!indice.Buscar(r.Origen, out io) || !indice.Buscar(r.Destino, out id)) continue;
                    g.DrawLines(casco, Tramo(pts, io, id));
                }
            }

            int alfa = camino != null ? 80 : 200;   // al mostrar una ruta, el resto de la red baja de intensidad
            foreach (Ruta r in rutas)
            {
                int io, id;
                if (!indice.Buscar(r.Origen, out io) || !indice.Buscar(r.Destino, out id)) continue;
                PointF[] poli = Tramo(pts, io, id);
                using (var lapiz = new Pen(Color.FromArgb(alfa, Tema.ColorZona(zonaDe[io])), 4f)
                       { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(lapiz, poli);
                if (r.Unidireccional) DibujarFlecha(g, poli, alfa);
            }
        }

        private static void DibujarFlecha(Graphics g, PointF[] poli, int alfa)
        {
            float angulo;
            PointF c = PuntoEn(poli, Longitud(poli) / 2f, out angulo);
            float ux = (float)Math.Cos(angulo), uy = (float)Math.Sin(angulo);
            float vx = -uy, vy = ux;
            var triangulo = new[]
            {
                new PointF(c.X + ux * 7, c.Y + uy * 7),
                new PointF(c.X - ux * 5 + vx * 6, c.Y - uy * 5 + vy * 6),
                new PointF(c.X - ux * 5 - vx * 6, c.Y - uy * 5 - vy * 6)
            };
            using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, alfa + 40), 255, 255, 255))) g.FillPolygon(b, triangulo);
        }

        private void DibujarCamino(Graphics g, PointF[] pts)
        {
            PointF[] poli = PolilineaCamino(pts);
            if (poli == null) return;

            float t = 1f - (float)Math.Pow(1f - progreso, 3);   // aparece rápido y frena al final
            if (t < 0.002f) return;
            PointF[] parcial = Recortar(poli, t);
            if (parcial.Length < 2) return;

            using (var casco = new Pen(Tema.ColMapa, 12f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(casco, parcial);
            using (var ruta = new Pen(Tema.ColAcento, 6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLines(ruta, parcial);

            if (progreso < 1f)   // el "bus" que va dibujando la ruta
            {
                PointF cabeza = parcial[parcial.Length - 1];
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cabeza.X - 7, cabeza.Y - 7, 14, 14);
                using (var p = new Pen(Tema.ColAcento, 3f)) g.DrawEllipse(p, cabeza.X - 7, cabeza.Y - 7, 14, 14);
            }
        }

        private bool EsEspecial(int i)
        {
            string c = paradas[i].Codigo;
            return (camino != null && ordenCamino.Contiene(c)) || (secuencia != null && ordenSecuencia.Contiene(c))
                   || c == seleccionada || i == hover;
        }

        private void DibujarParadas(Graphics g, PointF[] pts)
        {
            for (int pasada = 0; pasada < 2; pasada++)
                for (int i = 0; i < paradas.Length; i++)
                {
                    if ((pasada == 1) != EsEspecial(i)) continue;
                    DibujarParada(g, i, pts[i]);
                }
        }

        private void DibujarParada(Graphics g, int i, PointF c)
        {
            string codigo = paradas[i].Codigo;
            Color zona = Tema.ColorZona(zonaDe[i]);

            if (codigo == seleccionada || i == hover)
                using (var halo = new Pen(Color.FromArgb(i == hover ? 150 : 210, 255, 255, 255), 2f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(halo, c.X - 15, c.Y - 15, 30, 30);

            bool origen = camino != null && camino.Cantidad > 0 && camino[0] == codigo;
            bool destino = camino != null && camino.Cantidad > 0 && camino[camino.Cantidad - 1] == codigo;
            int orden = ordenCamino.Obtener(codigo);

            if (origen || destino)
            {
                if (origen ? progreso > 0.01f : progreso > 0.9f)
                {
                    Color fondo = origen ? Tema.ColTeal : Tema.ColCoral;
                    using (var b = new SolidBrush(fondo)) g.FillEllipse(b, c.X - 12, c.Y - 12, 24, 24);
                    using (var p = new Pen(Tema.ColMapa, 3f)) g.DrawEllipse(p, c.X - 12, c.Y - 12, 24, 24);
                    using (var t = new SolidBrush(Tema.ColFondo)) g.DrawString(origen ? "A" : "B", fPin, t, new RectangleF(c.X - 12, c.Y - 12, 24, 24), sfCentro);
                    return;
                }
            }
            if (camino != null && orden > 0)
            {
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, c.X - 8, c.Y - 8, 16, 16);
                using (var p = new Pen(Tema.ColAcento, 3.5f)) g.DrawEllipse(p, c.X - 8, c.Y - 8, 16, 16);
                return;
            }
            if (secuencia != null && ordenSecuencia.Contiene(codigo))
            {
                int k = ordenSecuencia.Obtener(codigo);
                using (var b = new SolidBrush(k == 1 ? Tema.ColTeal : Tema.ColAcento)) g.FillEllipse(b, c.X - 11, c.Y - 11, 22, 22);
                using (var p = new Pen(Tema.ColMapa, 2.5f)) g.DrawEllipse(p, c.X - 11, c.Y - 11, 22, 22);
                using (var t = new SolidBrush(Tema.ColFondo)) g.DrawString(k.ToString(), fPin, t, new RectangleF(c.X - 12, c.Y - 12, 24, 24), sfCentro);
                return;
            }

            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, c.X - 6.5f, c.Y - 6.5f, 13, 13);
            using (var p = new Pen(zona, 3.2f)) g.DrawEllipse(p, c.X - 6.5f, c.Y - 6.5f, 13, 13);
        }

        private void DibujarEtiquetas(Graphics g, PointF[] pts)
        {
            for (int i = 0; i < paradas.Length; i++)
            {
                if (paradas.Length > 40 && !EsEspecial(i)) continue;
                if (pts[i].X < -80 || pts[i].X > Width + 80 || pts[i].Y < -20 || pts[i].Y > Height + 20) continue;
                float x = pts[i].X + 12, y = pts[i].Y - 8;
                string t = paradas[i].Nombre;
                using (var halo = new SolidBrush(Color.FromArgb(230, Tema.ColMapa)))
                {
                    g.DrawString(t, fEtiqueta, halo, x - 1, y); g.DrawString(t, fEtiqueta, halo, x + 1, y);
                    g.DrawString(t, fEtiqueta, halo, x, y - 1); g.DrawString(t, fEtiqueta, halo, x, y + 1);
                }
                using (var b = new SolidBrush(EsEspecial(i) ? Color.White : Color.FromArgb(205, Tema.ColTexto))) g.DrawString(t, fEtiqueta, b, x, y);
            }
        }

        private void DibujarTarjetaHover(Graphics g, PointF[] pts)
        {
            if (hover < 0 || hover >= paradas.Length) return;
            Parada p = paradas[hover];
            string l1 = p.Nombre;
            string l2 = "Código " + p.Codigo + "   Zona " + zonaDe[hover] + "   " + gradoDe[hover] + (gradoDe[hover] == 1 ? " ruta" : " rutas");
            SizeF s1 = g.MeasureString(l1, fNegrita), s2 = g.MeasureString(l2, fEtiqueta);
            float w = Math.Max(s1.Width, s2.Width) + 24, h = 52;
            float x = Math.Min(Math.Max(8, pts[hover].X + 16), Width - w - 8);
            float y = Math.Min(Math.Max(8, pts[hover].Y - h - 14), Height - h - 8);
            using (var path = Tema.Redondeado(new RectangleF(x, y, w, h), 8))
            {
                using (var f = new SolidBrush(Color.FromArgb(240, Tema.ColPanel))) g.FillPath(f, path);
                using (var b = new Pen(Tema.ColBorde)) g.DrawPath(b, path);
            }
            using (var b = new SolidBrush(Tema.ColTexto)) g.DrawString(l1, fNegrita, b, x + 12, y + 8);
            using (var b = new SolidBrush(Tema.ColSuave)) g.DrawString(l2, fEtiqueta, b, x + 12, y + 29);
        }

        private void DibujarSuperposiciones(Graphics g)
        {
            // Resumen arriba a la izquierda
            string resumen = paradas.Length + (paradas.Length == 1 ? " parada" : " paradas") + "   " + rutas.Length +
                             (rutas.Length == 1 ? " ruta" : " rutas") + "   " + totalZonas + (totalZonas == 1 ? " zona" : " zonas");
            SizeF sr = g.MeasureString(resumen, fNegrita);
            using (var path = Tema.Redondeado(new RectangleF(16, 14, sr.Width + 26, 32), 8))
            {
                using (var f = new SolidBrush(Color.FromArgb(225, Tema.ColPanel))) g.FillPath(f, path);
                using (var b = new Pen(Tema.ColBorde)) g.DrawPath(b, path);
            }
            using (var b = new SolidBrush(Tema.ColTexto)) g.DrawString(resumen, fNegrita, b, 29, 21);

            // Leyenda de zonas abajo a la izquierda
            int mostrar = Math.Min(totalZonas, 6);
            float yl = Height - 16 - mostrar * 22;
            for (int z = 1; z <= mostrar; z++)
            {
                using (var b = new SolidBrush(Tema.ColorZona(z))) g.FillEllipse(b, 18, yl + (z - 1) * 22 + 4, 11, 11);
                using (var b = new SolidBrush(Tema.ColSuave))
                    g.DrawString("Zona " + z + ": " + conteoZona[z] + (conteoZona[z] == 1 ? " parada" : " paradas"), fEtiqueta, b, 36, yl + (z - 1) * 22 + 1);
            }

            // Botones de zoom
            for (int k = 0; k < 4; k++)
            {
                RectangleF r = BotonZoom(k);
                using (var path = Tema.Redondeado(r, 8))
                {
                    using (var f = new SolidBrush(Color.FromArgb(235, Tema.ColPanel))) g.FillPath(f, path);
                    using (var b = new Pen(Tema.ColBorde)) g.DrawPath(b, path);
                }
                using (var lapiz = new Pen(Tema.ColTexto, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                    if (k == 0) { g.DrawLine(lapiz, cx - 6, cy, cx + 6, cy); g.DrawLine(lapiz, cx, cy - 6, cx, cy + 6); }
                    else if (k == 1) g.DrawLine(lapiz, cx - 6, cy, cx + 6, cy);
                    else if (k == 2)
                    {
                        g.DrawRectangle(lapiz, cx - 7, cy - 7, 14, 14);
                        g.DrawEllipse(lapiz, cx - 2, cy - 2, 4, 4);
                    }
                    else
                    {
                        g.DrawEllipse(lapiz, cx - 8, cy - 8, 16, 16);      // globo: ver todo Guatemala
                        g.DrawEllipse(lapiz, cx - 3, cy - 8, 6, 16);
                        g.DrawLine(lapiz, cx - 8, cy, cx + 8, cy);
                    }
                }
            }

            if (ModoElegirUbicacion)
            {
                const string aviso = "Haz clic en el mapa para elegir la ubicación de la parada";
                SizeF sa = g.MeasureString(aviso, fNegrita);
                float x = (Width - sa.Width) / 2 - 14;
                using (var path = Tema.Redondeado(new RectangleF(x, Height - 62, sa.Width + 28, 36), 8))
                {
                    using (var f = new SolidBrush(Tema.ColAcento)) g.FillPath(f, path);
                }
                using (var b = new SolidBrush(Tema.ColFondo)) g.DrawString(aviso, fNegrita, b, x + 14, Height - 53);
            }
        }

        // ------------------------------------------------------------------ interacción

        private int ParadaEn(Point p)
        {
            int mejor = -1;
            double mejorD = 15 * 15;
            for (int i = 0; i < paradas.Length; i++)
            {
                PointF q = Pantalla(i);
                double dx = q.X - p.X, dy = q.Y - p.Y;
                double d = dx * dx + dy * dy;
                if (d <= mejorD) { mejorD = d; mejor = i; }
            }
            return mejor;
        }

        private int BotonEn(Point p)
        {
            for (int k = 0; k < 4; k++) if (BotonZoom(k).Contains(p)) return k;
            return -1;
        }

        private void Zoom(double factor, PointF centro)
        {
            double nueva = Math.Min(Math.Max(escala * factor, escalaFit * 0.004), escalaFit * 120);
            double f = nueva / escala;
            offX = centro.X - (centro.X - offX) * f;
            offY = centro.Y - (centro.Y - offY) * f;
            escala = nueva;
            vistaLibre = true;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left)
            {
                arrastrando = true; huboArrastre = false;
                ultimo = e.Location; inicioArrastre = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (arrastrando)
            {
                if (!huboArrastre && (Math.Abs(e.X - inicioArrastre.X) > 3 || Math.Abs(e.Y - inicioArrastre.Y) > 3)) huboArrastre = true;
                if (huboArrastre)
                {
                    offX += e.X - ultimo.X;
                    offY += e.Y - ultimo.Y;
                    ultimo = e.Location;
                    vistaLibre = true;
                    Invalidate();
                }
            }
            else
            {
                int h = ParadaEn(e.Location);
                if (h != hover) { hover = h; Invalidate(); }
                Cursor = ModoElegirUbicacion ? Cursors.Cross : (h >= 0 || BotonEn(e.Location) >= 0 ? Cursors.Hand : Cursors.Default);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hover != -1) { hover = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bool fueClic = arrastrando && !huboArrastre;
                arrastrando = false;
                if (fueClic) AlHacerClic(e.Location);
            }
            base.OnMouseUp(e);
        }

        private void AlHacerClic(Point p)
        {
            int boton = BotonEn(p);
            if (boton == 0) { Zoom(1.4, new PointF(Width / 2f, Height / 2f)); return; }
            if (boton == 1) { Zoom(1 / 1.4, new PointF(Width / 2f, Height / 2f)); return; }
            if (boton == 2) { vistaLibre = false; if (paradas.Length > 0) Reencuadrar(null); else EncuadrarPais(false); return; }
            if (boton == 3) { EncuadrarPais(true); return; }

            if (ModoElegirUbicacion)
            {
                double lon = lonRef + ((p.X - offX) / escala) / cosLat;
                double lat = latRef - (p.Y - offY) / escala;
                ModoElegirUbicacion = false;
                Cursor = Cursors.Default;
                Invalidate();
                if (UbicacionElegida != null) UbicacionElegida(lat, lon);
                return;
            }

            int i = ParadaEn(p);
            if (i >= 0)
            {
                seleccionada = paradas[i].Codigo;
                Invalidate();
                if (ParadaClick != null) ParadaClick(paradas[i].Codigo);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Zoom(Math.Pow(1.18, e.Delta / 120.0), e.Location);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (ParadaEn(e.Location) < 0 && BotonEn(e.Location) < 0) { vistaLibre = false; if (paradas.Length > 0) Reencuadrar(null); else EncuadrarPais(false); }
            base.OnMouseDoubleClick(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!vistaLibre) { if (paradas.Length > 0) Reencuadrar(null); else EncuadrarPais(false); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animacion.Dispose();
                fEtiqueta.Dispose(); fNegrita.Dispose(); fPin.Dispose(); fGrande.Dispose(); fSuave.Dispose(); sfCentro.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
