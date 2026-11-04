using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;
using TransporteUrbano.Servicios;

namespace TransporteUrbano.UI
{
    /// <summary>Marcador de una parada, dibujado a mano para tener los mismos colores por zona que el mapa ligero.</summary>
    internal class MarcadorParada : GMapMarker
    {
        private static readonly Font FEtiqueta = Tema.Fuente(8.5f, FontStyle.Bold);
        private static readonly Font FSuave = Tema.Fuente(8f);
        private static readonly Font FPin = Tema.Fuente(8.5f, FontStyle.Bold);
        private static readonly StringFormat SfCentro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        public string Codigo;
        public string Nombre;
        public int Zona;
        public int Grado;
        public int Estado;      // 0 normal, 1 en la ruta, 2 origen, 3 destino, 4 recorrido BFS/DFS
        public int Orden;
        public bool Seleccionada;
        public bool Hover;
        public bool MostrarEtiqueta;

        public MarcadorParada(PointLatLng posicion) : base(posicion)
        {
            Size = new Size(30, 30);
            Offset = new Point(-15, -15);
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = LocalPosition.X + Size.Width / 2f;
            float cy = LocalPosition.Y + Size.Height / 2f;
            bool especial = Estado != 0 || Seleccionada || Hover;

            if (Seleccionada || Hover)
                using (var halo = new Pen(Color.FromArgb(Hover ? 170 : 235, Tema.ColAcento), 2.2f) { DashStyle = DashStyle.Dot })
                    g.DrawEllipse(halo, cx - 15, cy - 15, 30, 30);

            if (Estado == 2 || Estado == 3)
            {
                using (var b = new SolidBrush(Estado == 2 ? Tema.ColTeal : Tema.ColCoral)) g.FillEllipse(b, cx - 12, cy - 12, 24, 24);
                using (var p = new Pen(Color.White, 3f)) g.DrawEllipse(p, cx - 12, cy - 12, 24, 24);
                using (var t = new SolidBrush(Tema.ColFondo)) g.DrawString(Estado == 2 ? "A" : "B", FPin, t, new RectangleF(cx - 12, cy - 12, 24, 24), SfCentro);
            }
            else if (Estado == 1)
            {
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - 8, cy - 8, 16, 16);
                using (var p = new Pen(Tema.ColAcento, 3.5f)) g.DrawEllipse(p, cx - 8, cy - 8, 16, 16);
            }
            else if (Estado == 4)
            {
                using (var b = new SolidBrush(Orden == 1 ? Tema.ColTeal : Tema.ColAcento)) g.FillEllipse(b, cx - 11, cy - 11, 22, 22);
                using (var p = new Pen(Color.White, 2.5f)) g.DrawEllipse(p, cx - 11, cy - 11, 22, 22);
                using (var t = new SolidBrush(Tema.ColFondo)) g.DrawString(Orden.ToString(), FPin, t, new RectangleF(cx - 12, cy - 12, 24, 24), SfCentro);
            }
            else
            {
                using (var borde = new Pen(Color.FromArgb(150, Tema.ColFondo), 1.5f)) g.DrawEllipse(borde, cx - 8.5f, cy - 8.5f, 17, 17);
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, cx - 6.5f, cy - 6.5f, 13, 13);
                using (var p = new Pen(Tema.ColorZona(Zona), 3.2f)) g.DrawEllipse(p, cx - 6.5f, cy - 6.5f, 13, 13);
            }

            if (MostrarEtiqueta || especial)
            {
                float x = cx + 14, y = cy - 8;
                DibujarTexto(g, Nombre, FEtiqueta, x, y, Tema.ColFondo);
                if (Hover)
                    DibujarTexto(g, "Zona " + Zona + ", " + Grado + (Grado == 1 ? " ruta" : " rutas"), FSuave, x, y + 14, Color.FromArgb(70, 90, 115));
            }
        }

        private static void DibujarTexto(Graphics g, string texto, Font f, float x, float y, Color color)
        {
            using (var halo = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            {
                g.DrawString(texto, f, halo, x - 1, y); g.DrawString(texto, f, halo, x + 1, y);
                g.DrawString(texto, f, halo, x, y - 1); g.DrawString(texto, f, halo, x, y + 1);
            }
            using (var b = new SolidBrush(color)) g.DrawString(texto, f, b, x, y);
        }
    }

    /// <summary>Flecha pequeña en el punto medio de una ruta de un solo sentido.</summary>
    internal class MarcadorFlecha : GMapMarker
    {
        private readonly double angulo;

        public MarcadorFlecha(PointLatLng posicion, double angulo) : base(posicion)
        {
            this.angulo = angulo;
            Size = new Size(16, 16);
            Offset = new Point(-8, -8);
            IsHitTestVisible = false;
        }

        public override void OnRender(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = LocalPosition.X + Size.Width / 2f, cy = LocalPosition.Y + Size.Height / 2f;
            float ux = (float)Math.Cos(angulo), uy = (float)Math.Sin(angulo), vx = -uy, vy = ux;
            var triangulo = new[]
            {
                new PointF(cx + ux * 7, cy + uy * 7),
                new PointF(cx - ux * 5 + vx * 6, cy - uy * 5 + vy * 6),
                new PointF(cx - ux * 5 - vx * 6, cy - uy * 5 - vy * 6)
            };
            using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, triangulo);
            using (var p = new Pen(Tema.ColFondo, 1.5f)) g.DrawPolygon(p, triangulo);
        }
    }

    /// <summary>Leyenda de zonas (colores de Union-Find) en una esquina del mapa.</summary>
    internal class LeyendaZonas : Control
    {
        private int[] conteo = new int[2];
        private int total;
        private readonly Font fuente = Tema.Fuente(8.5f);

        public LeyendaZonas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Tema.ColPanel;
            Width = 150;
        }

        public void Actualizar(int[] nuevoConteo, int totalZonas)
        {
            conteo = nuevoConteo;
            total = totalZonas;
            int n = Math.Min(total, 6);
            Visible = n > 0;
            Height = n * 20 + 14;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Tema.ColPanel);
            using (var borde = new Pen(Tema.ColBorde)) g.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
            int mostrar = Math.Min(total, 6);
            for (int z = 1; z <= mostrar; z++)
            {
                float y = 8 + (z - 1) * 20;
                using (var b = new SolidBrush(Tema.ColorZona(z))) g.FillEllipse(b, 10, y + 3, 11, 11);
                int n = z < conteo.Length ? conteo[z] : 0;
                using (var t = new SolidBrush(Tema.ColTexto)) g.DrawString("Zona " + z + ": " + n + (n == 1 ? " parada" : " paradas"), fuente, t, 28, y + 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fuente.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Mapa real de Guatemala (OpenStreetMap) con GMap.NET. Las teselas se descargan de internet y se guardan en
    /// una caché en disco: lo que ya se vio se puede volver a ver sin conexión. Las paradas, rutas, zonas y la ruta
    /// calculada se dibujan encima con los mismos colores que el mapa ligero.
    /// </summary>
    public class MapaGMapControl : Panel, IMapaRed
    {
        private readonly GMapControl gmap = new GMapControl();
        private readonly GMapOverlay capaRutas = new GMapOverlay("rutas");
        private readonly GMapOverlay capaCamino = new GMapOverlay("camino");
        private readonly GMapOverlay capaParadas = new GMapOverlay("paradas");

        private SistemaTransporte sistema;
        private Parada[] paradas = new Parada[0];
        private Ruta[] rutas = new Ruta[0];
        private MarcadorParada[] marcadores = new MarcadorParada[0];
        private readonly TablaHash<int> indice = new TablaHash<int>();

        private PointLatLng[] puntosCamino;
        private float progreso = 1f;
        private readonly Timer animacion = new Timer { Interval = 25 };
        private bool encuadrePendiente = true;

        private readonly Label resumen = new Label();
        private readonly Label aviso = new Label();
        private readonly Label atribucion = new Label();
        private readonly LeyendaZonas leyenda = new LeyendaZonas();
        private readonly Button btnMas, btnMenos, btnTodo, btnPais;

        public bool ModoElegirUbicacion { get; private set; }
        public event Action<string> ParadaClick;
        public event Action<double, double> UbicacionElegida;
        public Control Vista { get { return this; } }

        public MapaGMapControl()
        {
            BackColor = Tema.ColMapa;

            // --- el mapa
            GMapProvider.UserAgent = "TransporteUrbanoXela/1.0 (proyecto academico, Universidad Rafael Landivar)";
            GMaps.Instance.Mode = AccessMode.ServerAndCache;     // descarga y guarda en disco
            gmap.MapProvider = GMapProviders.OpenStreetMap;
            gmap.MinZoom = 5;
            gmap.MaxZoom = 19;
            gmap.Zoom = 7;
            gmap.Position = new PointLatLng(15.7, -90.35);
            gmap.MouseWheelZoomType = MouseWheelZoomType.MousePositionWithoutCenter;
            gmap.CanDragMap = true;
            gmap.DragButton = MouseButtons.Left;
            gmap.ShowCenter = false;
            gmap.Dock = DockStyle.Fill;
            gmap.Overlays.Add(capaRutas);
            gmap.Overlays.Add(capaCamino);
            gmap.Overlays.Add(capaParadas);

            gmap.OnMarkerClick += AlHacerClicEnMarcador;
            gmap.OnMarkerEnter += item => CambiarHover(item as MarcadorParada, true);
            gmap.OnMarkerLeave += item => CambiarHover(item as MarcadorParada, false);
            gmap.OnMapClick += AlHacerClicEnMapa;
            gmap.SizeChanged += (s, e) => EncuadrarSiCorresponde();

            // --- superposiciones (se agregan antes que el mapa para quedar por encima)
            resumen.AutoSize = true;
            resumen.BackColor = Tema.ColPanel;
            resumen.ForeColor = Tema.ColTexto;
            resumen.Font = Tema.Fuente(9.5f, FontStyle.Bold);
            resumen.Padding = new Padding(10, 7, 10, 7);
            resumen.Location = new Point(16, 14);

            aviso.AutoSize = true;
            aviso.BackColor = Tema.ColAcento;
            aviso.ForeColor = Tema.ColFondo;
            aviso.Font = Tema.Fuente(9.5f, FontStyle.Bold);
            aviso.Padding = new Padding(14, 9, 14, 9);
            aviso.Text = "Haz clic en el mapa para elegir la ubicación de la parada";
            aviso.Visible = false;

            atribucion.AutoSize = true;
            atribucion.BackColor = Color.FromArgb(235, 255, 255, 255);
            atribucion.ForeColor = Color.FromArgb(60, 70, 85);
            atribucion.Font = Tema.Fuente(7.5f);
            atribucion.Text = "© Colaboradores de OpenStreetMap";
            atribucion.Padding = new Padding(3, 1, 3, 1);

            btnMas = NuevoBoton("+", () => gmap.Zoom = Math.Min(gmap.MaxZoom, gmap.Zoom + 1));
            btnMenos = NuevoBoton("−", () => gmap.Zoom = Math.Max(gmap.MinZoom, gmap.Zoom - 1));
            btnTodo = NuevoBoton("Todo", () => { if (paradas.Length > 0) Reencuadrar(null); else EncuadrarPais(); });
            btnPais = NuevoBoton("País", EncuadrarPais);

            Controls.Add(resumen);
            Controls.Add(aviso);
            Controls.Add(atribucion);
            Controls.Add(leyenda);
            Controls.Add(btnMas);
            Controls.Add(btnMenos);
            Controls.Add(btnTodo);
            Controls.Add(btnPais);
            Controls.Add(gmap);

            animacion.Tick += (s, e) =>
            {
                progreso += 0.02f;
                if (progreso >= 1f) { progreso = 1f; animacion.Stop(); }
                DibujarCaminoParcial();
            };
            Resize += (s, e) => Reposicionar();
            Reposicionar();
        }

        private Button NuevoBoton(string texto, Action accion)
        {
            var b = Tema.Boton(texto, false, 60);
            b.Height = 32;
            b.Click += (s, e) => accion();
            return b;
        }

        private void Reposicionar()
        {
            leyenda.Location = new Point(16, Math.Max(60, Height - leyenda.Height - 34));
            atribucion.Location = new Point(4, Height - atribucion.Height - 4);
            aviso.Location = new Point(Math.Max(8, (Width - aviso.Width) / 2), Math.Max(8, Height - 70));
            Button[] botones = { btnMas, btnMenos, btnTodo, btnPais };
            for (int k = 0; k < botones.Length; k++)
            {
                botones[k].Location = new Point(Math.Max(8, Width - 76), Math.Max(60, Height - 22 - (botones.Length - k) * 36));
                botones[k].BringToFront();
            }
            leyenda.BringToFront();
            resumen.BringToFront();
            atribucion.BringToFront();
            aviso.BringToFront();
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
            animacion.Stop();
            puntosCamino = null;
            progreso = 1f;

            paradas = sistema.ListarParadas().ToArray();
            rutas = sistema.ListarRutas().ToArray();
            capaParadas.Markers.Clear();
            capaRutas.Routes.Clear();
            capaRutas.Markers.Clear();
            capaCamino.Routes.Clear();
            indice.Limpiar();

            TablaHash<int> grados = sistema.Grados();
            int totalZonas = sistema.CantidadZonas;
            int[] conteo = new int[totalZonas + 2];
            int[] zonaDe = new int[paradas.Length];
            marcadores = new MarcadorParada[paradas.Length];

            for (int i = 0; i < paradas.Length; i++)
            {
                Parada p = paradas[i];
                zonaDe[i] = sistema.NumeroZona(p.Codigo);
                if (zonaDe[i] < conteo.Length) conteo[zonaDe[i]]++;
                indice.Insertar(p.Codigo, i);
                var m = new MarcadorParada(new PointLatLng(p.Latitud, p.Longitud))
                {
                    Codigo = p.Codigo,
                    Nombre = p.Nombre,
                    Zona = zonaDe[i],
                    Grado = grados.Obtener(p.Codigo),
                    MostrarEtiqueta = paradas.Length <= 40
                };
                marcadores[i] = m;
                capaParadas.Markers.Add(m);
            }

            foreach (Ruta r in rutas)
            {
                int io, id;
                if (!indice.Buscar(r.Origen, out io) || !indice.Buscar(r.Destino, out id)) continue;
                PointLatLng a = marcadores[io].Position, b = marcadores[id].Position;
                var puntos = new List<PointLatLng> { a, b };
                var linea = new GMapRoute(puntos, r.Origen + ">" + r.Destino);
                linea.Stroke = new Pen(Color.FromArgb(215, Tema.ColorZona(zonaDe[io])), 4.5f);
                linea.Stroke.StartCap = LineCap.Round;
                linea.Stroke.EndCap = LineCap.Round;
                capaRutas.Routes.Add(linea);

                if (r.Unidireccional)
                {
                    double cos = Math.Cos((a.Lat + b.Lat) / 2 * Math.PI / 180);
                    double angulo = Math.Atan2(-(b.Lat - a.Lat), (b.Lng - a.Lng) * cos);
                    capaRutas.Markers.Add(new MarcadorFlecha(new PointLatLng((a.Lat + b.Lat) / 2, (a.Lng + b.Lng) / 2), angulo));
                }
            }

            resumen.Text = paradas.Length + (paradas.Length == 1 ? " parada" : " paradas") + "   " + rutas.Length +
                           (rutas.Length == 1 ? " ruta" : " rutas") + "   " + totalZonas + (totalZonas == 1 ? " zona" : " zonas");
            leyenda.Actualizar(conteo, totalZonas);
            Reposicionar();

            if (estabaVacio) encuadrePendiente = true;
            EncuadrarSiCorresponde();
            gmap.Refresh();
        }

        // ------------------------------------------------------------------ vista

        private void EncuadrarSiCorresponde()
        {
            if (!encuadrePendiente || gmap.Width < 200 || gmap.Height < 200) return;
            encuadrePendiente = false;
            if (paradas.Length > 0) Reencuadrar(null); else EncuadrarPais();
        }

        public void EncuadrarPais()
        {
            gmap.Position = new PointLatLng(15.7, -90.35);
            gmap.Zoom = 7;
        }

        /// <summary>Encuadra todas las paradas o solo las indicadas.</summary>
        public void Reencuadrar(ListaEnlazada<string> codigos)
        {
            if (paradas.Length == 0) { EncuadrarPais(); return; }
            double minLat = double.MaxValue, maxLat = double.MinValue, minLng = double.MaxValue, maxLng = double.MinValue;
            int contados = 0;
            for (int i = 0; i < marcadores.Length; i++)
            {
                if (codigos != null && !codigos.Existe(c => c == marcadores[i].Codigo)) continue;
                PointLatLng p = marcadores[i].Position;
                if (p.Lat < minLat) minLat = p.Lat;
                if (p.Lat > maxLat) maxLat = p.Lat;
                if (p.Lng < minLng) minLng = p.Lng;
                if (p.Lng > maxLng) maxLng = p.Lng;
                contados++;
            }
            if (contados == 0) return;

            double alto = maxLat - minLat, ancho = maxLng - minLng;
            if (alto < 0.0005 && ancho < 0.0005)
            {
                gmap.Position = new PointLatLng((minLat + maxLat) / 2, (minLng + maxLng) / 2);
                gmap.Zoom = 15;
                return;
            }
            double relleno = Math.Max(0.0015, Math.Max(alto, ancho) * 0.18);
            gmap.SetZoomToFitRect(RectLatLng.FromLTRB(minLng - relleno, maxLat + relleno, maxLng + relleno, minLat - relleno));
        }

        // ------------------------------------------------------------------ interacción

        private void CambiarHover(MarcadorParada m, bool valor)
        {
            if (m == null) return;
            m.Hover = valor;
            gmap.Invalidate();
        }

        private void AlHacerClicEnMarcador(GMapMarker item, MouseEventArgs e)
        {
            var m = item as MarcadorParada;
            if (m == null || e.Button != MouseButtons.Left) return;
            SeleccionarParada(m.Codigo, false);
            if (ParadaClick != null) ParadaClick(m.Codigo);
        }

        private void AlHacerClicEnMapa(PointLatLng punto, MouseEventArgs e)
        {
            if (!ModoElegirUbicacion || e.Button != MouseButtons.Left) return;
            ModoElegirUbicacion = false;
            aviso.Visible = false;
            if (UbicacionElegida != null) UbicacionElegida(punto.Lat, punto.Lng);
        }

        public void IniciarElegirUbicacion()
        {
            ModoElegirUbicacion = true;
            Reposicionar();
            aviso.Visible = true;
            aviso.BringToFront();
        }

        public void SeleccionarParada(string codigo, bool centrar)
        {
            for (int i = 0; i < marcadores.Length; i++) marcadores[i].Seleccionada = marcadores[i].Codigo == codigo;
            int k;
            if (centrar && codigo != null && indice.Buscar(codigo, out k))
            {
                gmap.Position = marcadores[k].Position;
                if (gmap.Zoom < 14) gmap.Zoom = 14;
            }
            gmap.Invalidate();
        }

        // ------------------------------------------------------------------ resaltados

        public void MostrarCamino(ListaEnlazada<string> codigos)
        {
            LimpiarResaltado();
            var puntos = new List<PointLatLng>();
            int k = 1;
            foreach (string c in codigos)
            {
                int i;
                if (!indice.Buscar(c, out i)) continue;
                marcadores[i].Orden = k;
                marcadores[i].Estado = k == 1 ? 2 : (k == codigos.Cantidad ? 3 : 1);
                puntos.Add(marcadores[i].Position);
                k++;
            }
            puntosCamino = puntos.ToArray();
            progreso = 0f;
            Reencuadrar(codigos);
            animacion.Start();
        }

        public void MostrarSecuencia(ListaEnlazada<string> codigos)
        {
            LimpiarResaltado();
            int k = 1;
            foreach (string c in codigos)
            {
                int i;
                if (!indice.Buscar(c, out i)) continue;
                marcadores[i].Estado = 4;
                marcadores[i].Orden = k++;
            }
            Reencuadrar(codigos);
            gmap.Refresh();
        }

        public void LimpiarResaltado()
        {
            animacion.Stop();
            puntosCamino = null;
            progreso = 1f;
            capaCamino.Routes.Clear();
            for (int i = 0; i < marcadores.Length; i++) { marcadores[i].Estado = 0; marcadores[i].Orden = 0; }
            gmap.Refresh();
        }

        /// <summary>Dibuja la ruta calculada "creciendo" desde el origen, como en el mapa ligero.</summary>
        private void DibujarCaminoParcial()
        {
            capaCamino.Routes.Clear();
            if (puntosCamino == null || puntosCamino.Length < 2) return;

            int n = puntosCamino.Length;
            double cos = Math.Cos(puntosCamino[0].Lat * Math.PI / 180);
            double[] tramo = new double[n - 1];
            double total = 0;
            for (int i = 0; i < n - 1; i++)
            {
                double dLat = puntosCamino[i + 1].Lat - puntosCamino[i].Lat;
                double dLng = (puntosCamino[i + 1].Lng - puntosCamino[i].Lng) * cos;
                tramo[i] = Math.Sqrt(dLat * dLat + dLng * dLng);
                total += tramo[i];
            }

            double t = 1 - Math.Pow(1 - progreso, 3);
            double objetivo = total * t;
            var parcial = new List<PointLatLng> { puntosCamino[0] };
            for (int i = 0; i < n - 1; i++)
            {
                if (objetivo >= tramo[i]) { parcial.Add(puntosCamino[i + 1]); objetivo -= tramo[i]; }
                else
                {
                    double f = tramo[i] <= 0 ? 0 : objetivo / tramo[i];
                    parcial.Add(new PointLatLng(
                        puntosCamino[i].Lat + (puntosCamino[i + 1].Lat - puntosCamino[i].Lat) * f,
                        puntosCamino[i].Lng + (puntosCamino[i + 1].Lng - puntosCamino[i].Lng) * f));
                    break;
                }
            }
            if (parcial.Count < 2) return;

            var casco = new GMapRoute(parcial, "casco");
            casco.Stroke = new Pen(Color.FromArgb(235, Tema.ColFondo), 11f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            var ruta = new GMapRoute(new List<PointLatLng>(parcial), "camino");
            ruta.Stroke = new Pen(Tema.ColAcento, 6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            capaCamino.Routes.Add(casco);
            capaCamino.Routes.Add(ruta);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animacion.Dispose();
            base.Dispose(disposing);
        }
    }
}
