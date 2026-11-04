using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TransporteUrbano.UI
{
    public class Kpi
    {
        public string Etiqueta;
        public string Valor;
        public Kpi(string etiqueta, string valor) { Etiqueta = etiqueta; Valor = valor; }
    }

    public class ItemLinea
    {
        public string Codigo;
        public string Titulo;
        public string Linea1;
        public string Linea2;
        public string Etiqueta;
        public int Estado;      // 0 intermedia, 1 origen/inicio, 2 destino
    }

    /// <summary>
    /// Franja inferior con el recorrido paso a paso (tarjetas de parada conectadas), como el panel de
    /// seguimiento de una flota. Se desplaza con la rueda del ratón o arrastrando.
    /// </summary>
    public class LineaTiempoControl : Control
    {
        private const int AnchoTarjeta = 184;
        private const int Espacio = 32;
        private const int Margen = 18;
        private const int AltoCabecera = 54;

        private string titulo = "Recorrido";
        private string detalle = "";
        private Kpi[] kpis = new Kpi[0];
        private ItemLinea[] items = new ItemLinea[0];
        private float scroll;
        private bool arrastrando, huboArrastre;
        private int xInicio;
        private float scrollInicio;

        private readonly Font fTitulo = Tema.Fuente(11.5f, FontStyle.Bold);
        private readonly Font fSuave = Tema.Fuente(9f);
        private readonly Font fKpiValor = Tema.Fuente(12.5f, FontStyle.Bold);
        private readonly Font fKpiEtiqueta = Tema.Fuente(8.5f);
        private readonly Font fNombre = Tema.Fuente(10.5f, FontStyle.Bold);
        private readonly Font fDato = Tema.Fuente(10f, FontStyle.Bold);
        private readonly StringFormat sfAjuste = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit };
        private readonly StringFormat sfCentro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        public event Action<string> ItemClick;

        public LineaTiempoControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Tema.ColPanel;
        }

        public void Mostrar(string nuevoTitulo, string nuevoDetalle, Kpi[] nuevosKpis, ItemLinea[] nuevosItems)
        {
            titulo = nuevoTitulo;
            detalle = nuevoDetalle ?? "";
            kpis = nuevosKpis ?? new Kpi[0];
            items = nuevosItems ?? new ItemLinea[0];
            scroll = 0;
            Invalidate();
        }

        public void Limpiar()
        {
            titulo = "Recorrido";
            detalle = "";
            kpis = new Kpi[0];
            items = new ItemLinea[0];
            scroll = 0;
            Invalidate();
        }

        private float AnchoTotal { get { return Margen * 2 + items.Length * AnchoTarjeta + Math.Max(0, items.Length - 1) * Espacio; } }
        private float ScrollMax { get { return Math.Max(0, AnchoTotal - Width); } }

        private static Color ColorEstado(int estado)
        {
            if (estado == 1) return Tema.ColTeal;
            if (estado == 2) return Tema.ColCoral;
            return Color.FromArgb(84, 118, 163);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Tema.ColPanel);
            using (var borde = new Pen(Tema.ColBorde)) g.DrawLine(borde, 0, 0, Width, 0);

            using (var bTexto = new SolidBrush(Tema.ColTexto))
            using (var bSuave = new SolidBrush(Tema.ColSuave))
            {
                g.DrawString(titulo, fTitulo, bTexto, Margen, 10);
                if (detalle.Length > 0) g.DrawString(detalle, fSuave, bSuave, Margen, 32);

                // KPIs alineados a la derecha
                float x = Width - Margen;
                for (int i = kpis.Length - 1; i >= 0; i--)
                {
                    SizeF sv = g.MeasureString(kpis[i].Valor, fKpiValor);
                    SizeF se = g.MeasureString(kpis[i].Etiqueta, fKpiEtiqueta);
                    float ancho = Math.Max(sv.Width, se.Width) + 28;
                    x -= ancho;
                    using (var path = Tema.Redondeado(new RectangleF(x, 8, ancho, 42), 8))
                    using (var fondo = new SolidBrush(Tema.ColPanelAlt))
                        g.FillPath(fondo, path);
                    g.DrawString(kpis[i].Etiqueta, fKpiEtiqueta, bSuave, x + 14, 11);
                    g.DrawString(kpis[i].Valor, fKpiValor, bTexto, x + 14, 24);
                    x -= 10;
                }

                if (items.Length == 0)
                {
                    var zona = new RectangleF(0, AltoCabecera, Width, Height - AltoCabecera);
                    g.DrawString("Calcula una ruta o ejecuta un recorrido para verlo parada por parada.", fSuave, bSuave, zona, sfCentro);
                    return;
                }

                float y = AltoCabecera + 6;
                float alto = Math.Max(90, Height - y - 20);
                for (int i = 0; i < items.Length; i++)
                {
                    float cx = Margen - scroll + i * (AnchoTarjeta + Espacio);
                    if (cx > Width || cx + AnchoTarjeta < 0) continue;
                    DibujarTarjeta(g, items[i], i, cx, y, alto, bTexto, bSuave);

                    if (i < items.Length - 1)
                    {
                        float ly = y + 28;
                        using (var lapiz = new Pen(Tema.ColBorde, 3f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
                        {
                            g.DrawLine(lapiz, cx + AnchoTarjeta + 4, ly, cx + AnchoTarjeta + Espacio - 4, ly);
                            g.DrawLine(lapiz, cx + AnchoTarjeta + Espacio - 10, ly - 5, cx + AnchoTarjeta + Espacio - 4, ly);
                            g.DrawLine(lapiz, cx + AnchoTarjeta + Espacio - 10, ly + 5, cx + AnchoTarjeta + Espacio - 4, ly);
                        }
                    }
                }

                if (ScrollMax > 0)
                {
                    float pista = Width - Margen * 2;
                    float pulgar = Math.Max(40, pista * Width / AnchoTotal);
                    float px = Margen + (pista - pulgar) * (scroll / ScrollMax);
                    using (var path = Tema.Redondeado(new RectangleF(px, Height - 10, pulgar, 4), 2))
                    using (var b = new SolidBrush(Tema.ColBorde))
                        g.FillPath(b, path);
                }
            }
        }

        private void DibujarTarjeta(Graphics g, ItemLinea it, int indice, float x, float y, float alto, Brush bTexto, Brush bSuave)
        {
            Color color = ColorEstado(it.Estado);
            var rect = new RectangleF(x, y, AnchoTarjeta, alto);
            using (var path = Tema.Redondeado(rect, 10))
            {
                using (var fondo = new SolidBrush(Tema.ColPanelAlt)) g.FillPath(fondo, path);
                var estado = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (var franja = new SolidBrush(color)) g.FillRectangle(franja, x, y, AnchoTarjeta, 5);
                g.Restore(estado);
            }

            var circulo = new RectangleF(x + 14, y + 17, 24, 24);
            using (var b = new SolidBrush(color)) g.FillEllipse(b, circulo);
            using (var texto = new SolidBrush(Tema.ColFondo)) g.DrawString((indice + 1).ToString(), fDato, texto, circulo, sfCentro);

            g.DrawString(it.Etiqueta ?? "", fSuave, bSuave, x + 46, y + 20);
            g.DrawString(it.Titulo ?? "", fNombre, bTexto, new RectangleF(x + 14, y + 50, AnchoTarjeta - 24, 42), sfAjuste);
            if (alto > 120)
            {
                g.DrawString(it.Linea1 ?? "", fDato, bTexto, x + 14, y + alto - 46);
                g.DrawString(it.Linea2 ?? "", fSuave, bSuave, x + 14, y + alto - 26);
            }
            else
            {
                g.DrawString((it.Linea1 ?? "") + "   " + (it.Linea2 ?? ""), fSuave, bSuave, x + 14, y + alto - 26);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll = Math.Max(0, Math.Min(ScrollMax, scroll - e.Delta / 2f));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { arrastrando = true; huboArrastre = false; xInicio = e.X; scrollInicio = scroll; }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (arrastrando)
            {
                if (Math.Abs(e.X - xInicio) > 4) huboArrastre = true;
                if (huboArrastre)
                {
                    scroll = Math.Max(0, Math.Min(ScrollMax, scrollInicio - (e.X - xInicio)));
                    Invalidate();
                }
            }
            Cursor = IndiceEn(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool fueClic = arrastrando && !huboArrastre;
            arrastrando = false;
            if (fueClic && e.Button == MouseButtons.Left)
            {
                int i = IndiceEn(e.Location);
                if (i >= 0 && ItemClick != null) ItemClick(items[i].Codigo);
            }
            base.OnMouseUp(e);
        }

        private int IndiceEn(Point p)
        {
            if (items.Length == 0 || p.Y < AltoCabecera) return -1;
            float rel = p.X + scroll - Margen;
            if (rel < 0) return -1;
            int i = (int)(rel / (AnchoTarjeta + Espacio));
            if (i >= items.Length) return -1;
            float dentro = rel - i * (AnchoTarjeta + Espacio);
            return dentro <= AnchoTarjeta ? i : -1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fTitulo.Dispose(); fSuave.Dispose(); fKpiValor.Dispose(); fKpiEtiqueta.Dispose();
                fNombre.Dispose(); fDato.Dispose(); sfAjuste.Dispose(); sfCentro.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
