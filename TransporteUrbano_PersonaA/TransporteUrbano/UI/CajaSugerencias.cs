using System;
using System.Drawing;
using System.Windows.Forms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;

namespace TransporteUrbano.UI
{
    /// <summary>ListBox que no roba el foco, para que escribir en la caja no se interrumpa al mostrar sugerencias.</summary>
    internal class ListaSinFoco : ListBox
    {
        public ListaSinFoco() { SetStyle(ControlStyles.Selectable, false); }
    }

    /// <summary>
    /// Cuadro de texto con autocompletado. Mientras se escribe consulta al Trie (ProveedorSugerencias);
    /// al elegir una sugerencia se queda con el CÓDIGO de la parada, que es la clave de la Tabla Hash.
    /// También acepta que se escriba el código directo (se resuelve con BuscadorPorCodigo).
    /// </summary>
    public class CajaSugerencias : Panel
    {
        private readonly TextBox caja = new TextBox();
        private readonly ListaSinFoco lista = new ListaSinFoco();
        private readonly ToolStripDropDown popup = new ToolStripDropDown();
        private readonly ToolStripControlHost host;
        private readonly Font fuenteNombre = Tema.Fuente(10f, FontStyle.Bold);
        private readonly Font fuenteCodigo = Tema.Fuente(9f);
        private Sugerencia[] actuales = new Sugerencia[0];
        private bool suprimir;

        public Func<string, ListaEnlazada<Sugerencia>> ProveedorSugerencias { get; set; }
        public Func<string, Parada> BuscadorPorCodigo { get; set; }
        public string CodigoSeleccionado { get; private set; }
        public event Action<string> ParadaElegida;
        public event Action TextoEditado;

        public string PlaceholderText
        {
            get { return caja.PlaceholderText; }
            set { caja.PlaceholderText = value; }
        }

        public string Texto { get { return caja.Text; } }

        public CajaSugerencias()
        {
            Height = 36;
            BackColor = Tema.ColPanelAlt;
            Padding = new Padding(10, 9, 10, 2);

            caja.BorderStyle = BorderStyle.None;
            caja.BackColor = Tema.ColPanelAlt;
            caja.ForeColor = Tema.ColTexto;
            caja.Font = Tema.Fuente(10f);
            caja.Dock = DockStyle.Fill;
            Controls.Add(caja);

            lista.BorderStyle = BorderStyle.None;
            lista.BackColor = Tema.ColPanelAlt;
            lista.DrawMode = DrawMode.OwnerDrawFixed;
            lista.ItemHeight = 30;
            lista.IntegralHeight = false;

            host = new ToolStripControlHost(lista) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
            popup.Items.Add(host);
            popup.Padding = Padding.Empty;
            popup.AutoClose = false;
            popup.DropShadowEnabled = false;
            popup.BackColor = Tema.ColPanelAlt;

            caja.TextChanged += (s, e) =>
            {
                if (suprimir) return;
                CodigoSeleccionado = null;
                if (TextoEditado != null) TextoEditado();
                ActualizarLista();
            };
            caja.KeyDown += AlTeclear;
            caja.LostFocus += (s, e) => OcultarLista();
            lista.MouseClick += (s, e) =>
            {
                int i = lista.IndexFromPoint(e.Location);
                if (i >= 0 && i < actuales.Length) Elegir(actuales[i]);
            };
            lista.DrawItem += DibujarItem;
        }

        private void ActualizarLista()
        {
            string texto = caja.Text;
            if (texto.Trim().Length == 0 || ProveedorSugerencias == null) { OcultarLista(); return; }

            actuales = ProveedorSugerencias(texto).ToArray();
            if (actuales.Length == 0) { OcultarLista(); return; }

            lista.BeginUpdate();
            lista.Items.Clear();
            foreach (Sugerencia s in actuales) lista.Items.Add(s);
            lista.EndUpdate();
            lista.SelectedIndex = 0;

            if (!IsHandleCreated || !Visible) return;
            int alto = Math.Min(actuales.Length, 8) * lista.ItemHeight + 2;
            lista.Size = new Size(Width, alto);
            host.Size = lista.Size;
            popup.Size = new Size(Width, alto);
            popup.Show(this, new Point(0, Height));
        }

        private void OcultarLista()
        {
            if (popup.Visible) popup.Close();
        }

        private void AlTeclear(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && popup.Visible)
            {
                lista.SelectedIndex = Math.Min(lista.Items.Count - 1, lista.SelectedIndex + 1);
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up && popup.Visible)
            {
                lista.SelectedIndex = Math.Max(0, lista.SelectedIndex - 1);
                e.Handled = true; e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                if (popup.Visible && actuales.Length > 0)
                {
                    Elegir(actuales[Math.Max(0, lista.SelectedIndex)]);
                }
                else
                {
                    string codigo = ObtenerCodigo();
                    if (codigo != null && ParadaElegida != null) ParadaElegida(codigo);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                OcultarLista();
            }
        }

        private void DibujarItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= actuales.Length) return;
            Sugerencia s = actuales[e.Index];
            bool activo = (e.State & DrawItemState.Selected) != 0;
            using (var fondo = new SolidBrush(activo ? Tema.ColBorde : Tema.ColPanelAlt))
                e.Graphics.FillRectangle(fondo, e.Bounds);

            var izq = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 80, e.Bounds.Height);
            var der = new Rectangle(e.Bounds.Right - 70, e.Bounds.Y, 62, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, s.Nombre, fuenteNombre, izq, Tema.ColTexto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, s.Codigo, fuenteCodigo, der, Tema.ColSuave,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void Elegir(Sugerencia s)
        {
            suprimir = true;
            caja.Text = s.Nombre;
            suprimir = false;
            caja.SelectionStart = caja.Text.Length;
            CodigoSeleccionado = s.Codigo;
            OcultarLista();
            if (ParadaElegida != null) ParadaElegida(s.Codigo);
        }

        /// <summary>Fija la parada desde fuera (por ejemplo al hacer clic en el mapa).</summary>
        public void Establecer(Parada p)
        {
            suprimir = true;
            caja.Text = p.Nombre;
            suprimir = false;
            CodigoSeleccionado = p.Codigo;
            OcultarLista();
            if (ParadaElegida != null) ParadaElegida(p.Codigo);
        }

        public void Limpiar()
        {
            suprimir = true;
            caja.Text = "";
            suprimir = false;
            CodigoSeleccionado = null;
            OcultarLista();
            if (TextoEditado != null) TextoEditado();
        }

        /// <summary>
        /// Código de la parada elegida. Si el usuario no eligió de la lista intenta resolver lo escrito:
        /// primero como código (Tabla Hash) y luego como nombre con una única coincidencia (Trie).
        /// </summary>
        public string ObtenerCodigo()
        {
            if (CodigoSeleccionado != null) return CodigoSeleccionado;
            string texto = caja.Text.Trim();
            if (texto.Length == 0) return null;

            if (BuscadorPorCodigo != null)
            {
                Parada p = BuscadorPorCodigo(texto);
                if (p != null)
                {
                    suprimir = true; caja.Text = p.Nombre; suprimir = false;
                    CodigoSeleccionado = p.Codigo;
                    return p.Codigo;
                }
            }
            if (ProveedorSugerencias != null)
            {
                ListaEnlazada<Sugerencia> r = ProveedorSugerencias(texto);
                if (r.Cantidad == 1)
                {
                    suprimir = true; caja.Text = r[0].Nombre; suprimir = false;
                    CodigoSeleccionado = r[0].Codigo;
                    return CodigoSeleccionado;
                }
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                popup.Dispose();
                fuenteNombre.Dispose();
                fuenteCodigo.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
