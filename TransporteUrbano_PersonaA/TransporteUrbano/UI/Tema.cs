using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace TransporteUrbano.UI
{
    /// <summary>
    /// Paleta y utilidades visuales. Idea: un plano de transporte nocturno. Azul tinta para la interfaz,
    /// el azafrán queda reservado para la ruta calculada (lo único que debe llamar la atención) y cada
    /// zona conectada tiene su propio color.
    /// </summary>
    public static class Tema
    {
        public static readonly Color ColFondo = ColorTranslator.FromHtml("#0E1A2B");
        public static readonly Color ColPanel = ColorTranslator.FromHtml("#14243A");
        public static readonly Color ColPanelAlt = ColorTranslator.FromHtml("#1C3250");
        public static readonly Color ColBorde = ColorTranslator.FromHtml("#2D4768");
        public static readonly Color ColTexto = ColorTranslator.FromHtml("#E8EEF6");
        public static readonly Color ColSuave = ColorTranslator.FromHtml("#8FA5C0");
        public static readonly Color ColAcento = ColorTranslator.FromHtml("#F5A524");   // la ruta calculada
        public static readonly Color ColTeal = ColorTranslator.FromHtml("#2EC4B6");
        public static readonly Color ColCoral = ColorTranslator.FromHtml("#EF6F6C");
        public static readonly Color ColMapa = ColorTranslator.FromHtml("#0B1626");

        public static readonly Color[] Zonas =
        {
            ColorTranslator.FromHtml("#4FA8F0"),
            ColorTranslator.FromHtml("#9A86F5"),
            ColorTranslator.FromHtml("#E77BB6"),
            ColorTranslator.FromHtml("#8ED65A"),
            ColorTranslator.FromHtml("#5ADBD0"),
            ColorTranslator.FromHtml("#E8C547")
        };

        public static Color ColorZona(int numero)
        {
            int n = Math.Max(1, numero) - 1;
            return Zonas[n % Zonas.Length];
        }

        private static string familia;

        private static string Familia
        {
            get
            {
                if (familia != null) return familia;
                familia = "Segoe UI";
                using (var instaladas = new InstalledFontCollection())
                {
                    foreach (FontFamily f in instaladas.Families)
                        if (f.Name == "Bahnschrift") { familia = "Bahnschrift"; break; }   // tipografía de señalética
                }
                return familia;
            }
        }

        public static Font Fuente(float puntos, FontStyle estilo = FontStyle.Regular)
        {
            return new Font(Familia, puntos, estilo, GraphicsUnit.Point);
        }

        public static GraphicsPath Redondeado(RectangleF r, float radio)
        {
            float d = radio * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Button Boton(string texto, bool primario, int ancho)
        {
            var b = new Button
            {
                Text = texto,
                Width = ancho,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                BackColor = primario ? ColAcento : ColPanelAlt,
                ForeColor = primario ? ColFondo : ColTexto,
                Font = Fuente(9.5f, primario ? FontStyle.Bold : FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = primario ? Color.FromArgb(255, 192, 72) : ColBorde;
            return b;
        }

        public static Label Etiqueta(string texto, float puntos, bool suave, bool negrita)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                ForeColor = suave ? ColSuave : ColTexto,
                BackColor = Color.Transparent,
                Font = Fuente(puntos, negrita ? FontStyle.Bold : FontStyle.Regular)
            };
        }

        /// <summary>Campo de texto plano con fondo propio y texto de ayuda.</summary>
        public static Panel Campo(string ayuda, int ancho, out TextBox caja)
        {
            var panel = new Panel { Width = ancho, Height = 34, BackColor = ColPanelAlt, Padding = new Padding(10, 8, 10, 2) };
            caja = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = ColPanelAlt,
                ForeColor = ColTexto,
                Font = Fuente(10f),
                Dock = DockStyle.Fill,
                PlaceholderText = ayuda
            };
            panel.Controls.Add(caja);
            return panel;
        }
    }
}
