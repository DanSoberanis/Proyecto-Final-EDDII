using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;
using TransporteUrbano.Servicios;

namespace TransporteUrbano.UI
{
    /// <summary>
    /// [PERSONA A] Interfaz gráfica. Distribución:
    ///   arriba   : título, búsqueda global con autocompletado (Trie), carga/guardado de archivos
    ///   izquierda: cuatro pestañas (Paradas, Rutas, Calcular, Análisis)
    ///   centro   : mapa de la red
    ///   abajo    : recorrido paso a paso
    /// </summary>
    public class FormPrincipal : Form
    {
        private const int AnchoCol = 330;

        private readonly SistemaTransporte sistema;
        private IMapaRed mapa;
        private Control mapaVista;
        private Button btnMapa;
        private string avisoMapaInicial;
        private LineaTiempoControl linea;

        // aviso flotante sobre el mapa
        private Panel toast, toastBanda;
        private Label toastTexto;
        private Timer toastTimer;

        // barra superior
        private CajaSugerencias cajaGlobal;
        private Label lblGrafo;

        // navegación lateral
        private Panel panelIzq;
        private readonly Panel[] paginas = new Panel[4];
        private readonly Button[] botonesNav = new Button[4];
        private int paginaActual;
        private string seleccionActual;

        // página Paradas
        private CajaSugerencias cajaBuscar;
        private Label lblDetalleTitulo, lblDetalleCuerpo, lblTituloLista;
        private Button btnVerMapa, btnEliminarParada;
        private TextBox txtCodigo, txtNombre, txtLat, txtLon;
        private ListView lvParadas;
        private bool llenandoLista;

        // página Rutas
        private CajaSugerencias rutaOrigen, rutaDestino;
        private TextBox txtTiempo, txtCosto;
        private CheckBox chkUnSentido;
        private Label lblZonaRuta;
        private ListView lvRutas;

        // página Calcular
        private CajaSugerencias calcOrigen, calcDestino;
        private RadioButton rbTiempo, rbCosto;
        private TextBox txtPasos;
        private Label lblResumen;

        // página Análisis
        private CajaSugerencias recInicio;
        private TextBox txtSalida;

        public FormPrincipal(SistemaTransporte sistema)
        {
            this.sistema = sistema;

            Text = "Red de transporte urbano";
            Font = Tema.Fuente(9.5f);
            BackColor = Tema.ColFondo;
            ForeColor = Tema.ColTexto;
            Size = new Size(1440, 860);
            MinimumSize = new Size(1120, 720);
            StartPosition = FormStartPosition.CenterScreen;

            // El control Fill va primero; los demás se acoplan a su alrededor.
            ConstruirMapa();
            ConstruirLineaTiempo();
            ConstruirPanelIzquierdo();
            ConstruirBarraSuperior();

            sistema.DatosCambiaron += RefrescarTodo;
            Shown += (s, e) => { CargaInicial(); if (avisoMapaInicial != null) Avisar(avisoMapaInicial, true); };
            IrAPagina(0);
            ActualizarEstadoGrafo();
        }

        // ================================================================== construcción

        private void ConstruirMapa()
        {
            toast = new Panel { Width = 560, Height = 52, BackColor = Tema.ColPanel, Visible = false };
            toastBanda = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = Tema.ColTeal };
            toastTexto = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 0, 10, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Tema.ColTexto,
                Font = Tema.Fuente(9.5f)
            };
            toast.Controls.Add(toastTexto);
            toast.Controls.Add(toastBanda);

            toastTimer = new Timer { Interval = 4500 };
            toastTimer.Tick += (s, e) => { toast.Visible = false; toastTimer.Stop(); };

            // Mapa real (GMap.NET) si se puede; si no, el mapa ligero que siempre funciona.
            AsignarMapa(FabricaMapa.Crear(sistema, out avisoMapaInicial));
        }

        /// <summary>Pone un mapa en la ventana (y quita el anterior si lo había).</summary>
        private void AsignarMapa(IMapaRed nuevo)
        {
            if (mapaVista != null)
            {
                mapaVista.Controls.Remove(toast);   // el aviso no debe destruirse junto con el mapa viejo
                Controls.Remove(mapaVista);
                mapaVista.Dispose();
            }
            mapa = nuevo;
            mapaVista = nuevo.Vista;
            mapaVista.Dock = DockStyle.Fill;
            mapa.ParadaClick += AlClicEnParada;
            mapa.UbicacionElegida += AlElegirUbicacion;

            Controls.Add(mapaVista);
            mapaVista.BringToFront();       // el control Fill debe quedar al frente para repartirse el espacio sobrante
            mapaVista.Controls.Add(toast);
            toast.BringToFront();
            mapaVista.Resize += (s, e) => toast.Left = Math.Max(8, (mapaVista.Width - toast.Width) / 2);
        }

        private void AlElegirUbicacion(double lat, double lon)
        {
            txtLat.Text = lat.ToString("0.000000", CultureInfo.InvariantCulture);
            txtLon.Text = lon.ToString("0.000000", CultureInfo.InvariantCulture);
            IrAPagina(0);
            Avisar("Ubicación elegida. Completa el código y el nombre y registra la parada.");
        }

        private void CambiarMapa()
        {
            FabricaMapa.UsarMapaReal = !FabricaMapa.UsarMapaReal;
            string aviso;
            AsignarMapa(FabricaMapa.Crear(sistema, out aviso));
            btnMapa.Text = FabricaMapa.UsarMapaReal ? "Mapa ligero" : "Mapa real";
            if (seleccionActual != null) mapa.SeleccionarParada(seleccionActual, false);
            Avisar(aviso ?? (FabricaMapa.UsarMapaReal
                ? "Mapa real activado. Necesita internet para descargar el fondo; lo ya visto queda guardado."
                : "Mapa ligero activado. Funciona sin internet."), aviso != null);
        }

        private void ConstruirLineaTiempo()
        {
            linea = new LineaTiempoControl { Dock = DockStyle.Bottom, Height = 200 };
            linea.ItemClick += codigo =>
            {
                seleccionActual = codigo;
                mapa.SeleccionarParada(codigo, true);
            };
            Controls.Add(linea);
        }

        private void ConstruirBarraSuperior()
        {
            var barra = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Tema.ColPanel };

            var titulo = Tema.Etiqueta("Red de transporte urbano", 15f, false, true);
            titulo.Location = new Point(20, 9);
            var sub = Tema.Etiqueta("Paradas, rutas y zonas conectadas", 9f, true, false);
            sub.Location = new Point(22, 40);
            barra.Controls.Add(titulo);
            barra.Controls.Add(sub);

            cajaGlobal = NuevaCaja("Buscar una parada por nombre", 340);
            cajaGlobal.Location = new Point(380, 15);
            cajaGlobal.ParadaElegida += codigo =>
            {
                seleccionActual = codigo;
                mapa.SeleccionarParada(codigo, true);
                IrAPagina(0);
                MostrarDetalle(codigo);
            };
            barra.Controls.Add(cajaGlobal);

            var acciones = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 16, 16, 0),
                BackColor = Tema.ColPanel
            };

            lblGrafo = new Label
            {
                AutoSize = false,
                Width = 170,
                Height = 34,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fuente(9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 16, 0)
            };
            var btnParadas = Tema.Boton("Cargar paradas", false, 120);
            var btnRutas = Tema.Boton("Cargar rutas", false, 105);
            var btnGuardar = Tema.Boton("Guardar datos", false, 108);
            btnMapa = Tema.Boton(FabricaMapa.UsarMapaReal ? "Mapa ligero" : "Mapa real", false, 108);
            btnMapa.Click += (s, e) => CambiarMapa();
            btnParadas.Click += (s, e) => CargarArchivo(true);
            btnRutas.Click += (s, e) => CargarArchivo(false);
            btnGuardar.Click += (s, e) => GuardarDatos();
            acciones.Controls.Add(lblGrafo);
            acciones.Controls.Add(btnParadas);
            acciones.Controls.Add(btnRutas);
            acciones.Controls.Add(btnGuardar);
            acciones.Controls.Add(btnMapa);
            barra.Controls.Add(acciones);

            Controls.Add(barra);
        }

        private void ConstruirPanelIzquierdo()
        {
            panelIzq = new Panel { Dock = DockStyle.Left, Width = 384, BackColor = Tema.ColPanel };

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = Tema.ColPanel,
                WrapContents = false,
                Padding = new Padding(8, 6, 0, 0)
            };
            string[] nombres = { "Paradas", "Rutas", "Calcular", "Análisis" };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = new Button
                {
                    Text = nombres[i],
                    Width = 90,
                    Height = 34,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    UseVisualStyleBackColor = false,
                    Margin = new Padding(0, 0, 4, 0),
                    Font = Tema.Fuente(10f, FontStyle.Bold)
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (s, e) => IrAPagina(k);
                botonesNav[i] = b;
                nav.Controls.Add(b);
            }

            var contenedor = new Panel { Dock = DockStyle.Fill, BackColor = Tema.ColPanel };
            for (int i = 0; i < 4; i++)
            {
                paginas[i] = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 8, 10), Visible = false, BackColor = Tema.ColPanel };
                contenedor.Controls.Add(paginas[i]);
            }
            ConstruirPaginaParadas(paginas[0]);
            ConstruirPaginaRutas(paginas[1]);
            ConstruirPaginaCalcular(paginas[2]);
            ConstruirPaginaAnalisis(paginas[3]);

            panelIzq.Controls.Add(contenedor);
            panelIzq.Controls.Add(nav);
            Controls.Add(panelIzq);
        }

        // ---------------------------------------------------------------- helpers de construcción

        private CajaSugerencias NuevaCaja(string ayuda, int ancho)
        {
            var c = new CajaSugerencias { Width = ancho, PlaceholderText = ayuda };
            c.ProveedorSugerencias = sistema.SugerirParadas;     // Trie
            c.BuscadorPorCodigo = sistema.BuscarParada;          // Tabla Hash
            return c;
        }

        private FlowLayoutPanel NuevaColumna(Panel pagina)
        {
            var f = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Tema.ColPanel
            };
            pagina.Controls.Add(f);
            return f;
        }

        private static void Poner(FlowLayoutPanel f, Control c, int arriba)
        {
            c.Margin = new Padding(0, arriba, 0, 0);
            f.Controls.Add(c);
        }

        private static Label Titulo(string texto)
        {
            return Tema.Etiqueta(texto, 11.5f, false, true);
        }

        private static FlowLayoutPanel Fila(int alto)
        {
            return new FlowLayoutPanel
            {
                Width = AnchoCol,
                Height = alto,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Tema.ColPanel
            };
        }

        private static ListView NuevaLista(int alto, string[] titulos, int[] anchos)
        {
            var lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.None,
                BorderStyle = BorderStyle.None,
                BackColor = Tema.ColPanelAlt,
                ForeColor = Tema.ColTexto,
                Font = Tema.Fuente(9.5f),
                Width = AnchoCol,
                Height = alto
            };
            for (int i = 0; i < titulos.Length; i++) lv.Columns.Add(titulos[i], anchos[i]);
            return lv;
        }

        private static bool TryDouble(string texto, out double valor)
        {
            return double.TryParse((texto ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
        }

        // ---------------------------------------------------------------- página Paradas

        private void ConstruirPaginaParadas(Panel pagina)
        {
            FlowLayoutPanel f = NuevaColumna(pagina);

            Poner(f, Titulo("Buscar parada"), 0);
            cajaBuscar = NuevaCaja("Escribe el nombre: Plaza, Terminal…", AnchoCol);
            cajaBuscar.ParadaElegida += codigo =>
            {
                seleccionActual = codigo;
                mapa.SeleccionarParada(codigo, true);
                MostrarDetalle(codigo);
            };
            Poner(f, cajaBuscar, 8);

            var tarjeta = new Panel { Width = AnchoCol, Height = 136, BackColor = Tema.ColPanelAlt, Padding = new Padding(14, 12, 14, 8) };
            lblDetalleTitulo = new Label { Dock = DockStyle.Top, Height = 26, Font = Tema.Fuente(11.5f, FontStyle.Bold), ForeColor = Tema.ColTexto };
            lblDetalleCuerpo = new Label { Dock = DockStyle.Fill, Font = Tema.Fuente(9.5f), ForeColor = Tema.ColSuave };
            var botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36, WrapContents = false, BackColor = Tema.ColPanelAlt };
            btnVerMapa = Tema.Boton("Ver en el mapa", false, 130);
            btnVerMapa.BackColor = Tema.ColBorde;
            btnEliminarParada = Tema.Boton("Eliminar", false, 90);
            btnEliminarParada.BackColor = Tema.ColBorde;
            btnVerMapa.Click += (s, e) => { if (seleccionActual != null) mapa.SeleccionarParada(seleccionActual, true); };
            btnEliminarParada.Click += (s, e) => EliminarParadaSeleccionada();
            botones.Controls.Add(btnVerMapa);
            botones.Controls.Add(btnEliminarParada);
            tarjeta.Controls.Add(lblDetalleCuerpo);
            tarjeta.Controls.Add(lblDetalleTitulo);
            tarjeta.Controls.Add(botones);
            Poner(f, tarjeta, 10);
            MostrarDetalle(null);

            Poner(f, Titulo("Nueva parada"), 18);
            var filaCodigo = Fila(36);
            Panel pCodigo = Tema.Campo("Código (P020)", 110, out txtCodigo);
            Panel pNombre = Tema.Campo("Nombre de la parada", 210, out txtNombre);
            pCodigo.Margin = new Padding(0, 0, 10, 0);
            pNombre.Margin = new Padding(0);
            filaCodigo.Controls.Add(pCodigo);
            filaCodigo.Controls.Add(pNombre);
            Poner(f, filaCodigo, 8);

            var filaCoord = Fila(36);
            Panel pLat = Tema.Campo("Latitud", 160, out txtLat);
            Panel pLon = Tema.Campo("Longitud", 160, out txtLon);
            pLat.Margin = new Padding(0, 0, 10, 0);
            pLon.Margin = new Padding(0);
            filaCoord.Controls.Add(pLat);
            filaCoord.Controls.Add(pLon);
            Poner(f, filaCoord, 0);

            var filaBtn = Fila(38);
            var btnElegir = Tema.Boton("Elegir en el mapa", false, 150);
            var btnRegistrar = Tema.Boton("Registrar parada", true, 150);
            btnElegir.Click += (s, e) =>
            {
                mapa.IniciarElegirUbicacion();
                Avisar("Haz clic en el mapa para elegir la ubicación.");
            };
            btnRegistrar.Click += (s, e) => RegistrarParada();
            filaBtn.Controls.Add(btnElegir);
            filaBtn.Controls.Add(btnRegistrar);
            Poner(f, filaBtn, 2);

            lblTituloLista = Titulo("Paradas registradas");
            Poner(f, lblTituloLista, 16);
            lvParadas = NuevaLista(230, new[] { "Código", "Nombre", "Zona" }, new[] { 70, 200, 50 });
            lvParadas.SelectedIndexChanged += (s, e) =>
            {
                if (llenandoLista || lvParadas.SelectedItems.Count == 0) return;
                string codigo = (string)lvParadas.SelectedItems[0].Tag;
                seleccionActual = codigo;
                mapa.SeleccionarParada(codigo, true);
                MostrarDetalle(codigo);
            };
            Poner(f, lvParadas, 8);
        }

        // ---------------------------------------------------------------- página Rutas

        private void ConstruirPaginaRutas(Panel pagina)
        {
            FlowLayoutPanel f = NuevaColumna(pagina);

            Poner(f, Titulo("Habilitar una ruta nueva"), 0);
            Poner(f, Tema.Etiqueta("Origen", 9f, true, false), 8);
            rutaOrigen = NuevaCaja("Nombre o código de la parada", AnchoCol);
            Poner(f, rutaOrigen, 2);
            Poner(f, Tema.Etiqueta("Destino", 9f, true, false), 8);
            rutaDestino = NuevaCaja("Nombre o código de la parada", AnchoCol);
            Poner(f, rutaDestino, 2);
            rutaOrigen.ParadaElegida += c => ActualizarZonaRuta();
            rutaDestino.ParadaElegida += c => ActualizarZonaRuta();
            rutaOrigen.TextoEditado += ActualizarZonaRuta;
            rutaDestino.TextoEditado += ActualizarZonaRuta;

            lblZonaRuta = new Label
            {
                Width = AnchoCol,
                Height = 44,
                ForeColor = Tema.ColSuave,
                Font = Tema.Fuente(9.5f)
            };
            Poner(f, lblZonaRuta, 8);
            ActualizarZonaRuta();

            var filaNum = Fila(36);
            Panel pT = Tema.Campo("Tiempo (min)", 160, out txtTiempo);
            Panel pC = Tema.Campo("Costo (Q)", 160, out txtCosto);
            pT.Margin = new Padding(0, 0, 10, 0);
            pC.Margin = new Padding(0);
            filaNum.Controls.Add(pT);
            filaNum.Controls.Add(pC);
            Poner(f, filaNum, 4);

            chkUnSentido = new CheckBox
            {
                Text = "Solo un sentido (origen hacia destino)",
                AutoSize = true,
                ForeColor = Tema.ColTexto,
                Font = Tema.Fuente(9.5f)
            };
            Poner(f, chkUnSentido, 10);

            var btnHabilitar = Tema.Boton("Habilitar ruta", true, 150);
            btnHabilitar.Click += (s, e) => RegistrarRuta();
            Poner(f, btnHabilitar, 12);

            lvRutas = NuevaLista(200, new[] { "Origen", "Destino", "Min", "Q", "" }, new[] { 66, 66, 52, 56, 70 });
            Poner(f, Titulo("Rutas habilitadas"), 18);
            Poner(f, lvRutas, 8);
            var btnQuitar = Tema.Boton("Eliminar la ruta elegida", false, 200);
            btnQuitar.Click += (s, e) => EliminarRutaSeleccionada();
            Poner(f, btnQuitar, 8);
        }

        // ---------------------------------------------------------------- página Calcular

        private void ConstruirPaginaCalcular(Panel pagina)
        {
            FlowLayoutPanel f = NuevaColumna(pagina);

            Poner(f, Titulo("Calcular la mejor ruta"), 0);
            Poner(f, Tema.Etiqueta("Origen", 9f, true, false), 8);
            calcOrigen = NuevaCaja("Escribe o haz clic en el mapa", AnchoCol);
            Poner(f, calcOrigen, 2);
            Poner(f, Tema.Etiqueta("Destino", 9f, true, false), 8);
            calcDestino = NuevaCaja("Escribe o haz clic en el mapa", AnchoCol);
            Poner(f, calcDestino, 2);

            rbTiempo = new RadioButton { Text = "Más rápida (menor tiempo)", Checked = true, AutoSize = true, ForeColor = Tema.ColTexto, Font = Tema.Fuente(9.5f) };
            rbCosto = new RadioButton { Text = "Más económica (menor costo)", AutoSize = true, ForeColor = Tema.ColTexto, Font = Tema.Fuente(9.5f) };
            Poner(f, rbTiempo, 12);
            Poner(f, rbCosto, 4);

            var btnCalcular = Tema.Boton("Calcular ruta", true, 160);
            btnCalcular.Click += (s, e) => CalcularRuta();
            Poner(f, btnCalcular, 12);

            lblResumen = new Label { Width = AnchoCol, Height = 30, Font = Tema.Fuente(11f, FontStyle.Bold), ForeColor = Tema.ColAcento };
            Poner(f, lblResumen, 14);

            Poner(f, Titulo("Cómo se calculó"), 4);
            txtPasos = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Tema.ColPanelAlt,
                ForeColor = Tema.ColTexto,
                Font = Tema.Fuente(9.5f),
                Width = AnchoCol,
                Height = 150,
                ScrollBars = ScrollBars.Vertical,
                Text = "Aquí verás qué estructura participó en cada paso: Tabla Hash, Union-Find y Grafo."
            };
            Poner(f, txtPasos, 8);
        }

        // ---------------------------------------------------------------- página Análisis

        private void ConstruirPaginaAnalisis(Panel pagina)
        {
            txtSalida = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Tema.ColPanelAlt,
                ForeColor = Tema.ColTexto,
                Font = new Font("Consolas", 9.5f),
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = "Ejecuta un recorrido o abre un reporte para ver el resultado aquí."
            };
            pagina.Controls.Add(txtSalida);

            var arriba = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Tema.ColPanel
            };

            Poner(arriba, Titulo("Recorridos del grafo"), 0);
            Poner(arriba, Tema.Etiqueta("Parada de inicio", 9f, true, false), 6);
            recInicio = NuevaCaja("Nombre o código de la parada", AnchoCol);
            Poner(arriba, recInicio, 2);

            var filaRec = Fila(40);
            var btnBfs = Tema.Boton("Recorrido BFS", false, 150);
            var btnDfs = Tema.Boton("Recorrido DFS", false, 150);
            btnBfs.Click += (s, e) => EjecutarRecorrido(true);
            btnDfs.Click += (s, e) => EjecutarRecorrido(false);
            filaRec.Controls.Add(btnBfs);
            filaRec.Controls.Add(btnDfs);
            Poner(arriba, filaRec, 8);

            Poner(arriba, Titulo("Reportes y estructuras"), 8);
            var reportes = new FlowLayoutPanel { Width = AnchoCol, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, BackColor = Tema.ColPanel };
            AgregarReporte(reportes, "Zonas conectadas", () => sistema.ReporteZonas());
            AgregarReporte(reportes, "Más conectadas", () => sistema.ReporteMasConectadas(8));
            AgregarReporte(reportes, "Estructuras", () => sistema.ResumenEstructuras());
            AgregarReporte(reportes, "Tabla Hash", () => sistema.ImprimirTablaHash());
            AgregarReporte(reportes, "Trie", () => sistema.ImprimirTrie());
            AgregarReporte(reportes, "Grafo", () => sistema.ImprimirGrafo());
            Poner(arriba, reportes, 8);

            pagina.Controls.Add(arriba);
        }

        private void AgregarReporte(FlowLayoutPanel destino, string texto, Func<string> generar)
        {
            var b = Tema.Boton(texto, false, 156);
            b.Margin = new Padding(0, 0, 8, 8);
            b.Click += (s, e) => Salida(generar());
            destino.Controls.Add(b);
        }

        private void Salida(string texto)
        {
            txtSalida.Text = (texto ?? "").Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            txtSalida.SelectionStart = 0;
            txtSalida.ScrollToCaret();
        }

        // ================================================================== navegación y refresco

        private void IrAPagina(int k)
        {
            paginaActual = k;
            for (int i = 0; i < 4; i++)
            {
                paginas[i].Visible = i == k;
                botonesNav[i].BackColor = i == k ? Tema.ColPanelAlt : Tema.ColPanel;
                botonesNav[i].ForeColor = i == k ? Tema.ColAcento : Tema.ColSuave;
            }
        }

        private void RefrescarTodo()
        {
            mapa.LimpiarResaltado();
            linea.Limpiar();
            mapa.Refrescar();
            LlenarListaParadas();
            LlenarListaRutas();
            ActualizarEstadoGrafo();
            ActualizarZonaRuta();

            if (seleccionActual != null && sistema.BuscarParada(seleccionActual) == null) seleccionActual = null;
            MostrarDetalle(seleccionActual);
        }

        private void ActualizarEstadoGrafo()
        {
            if (sistema.GrafoDisponible)
            {
                lblGrafo.Text = "Grafo conectado";
                lblGrafo.BackColor = Tema.ColTeal;
                lblGrafo.ForeColor = Tema.ColFondo;
            }
            else
            {
                lblGrafo.Text = "Grafo pendiente (Persona B)";
                lblGrafo.BackColor = Tema.ColCoral;
                lblGrafo.ForeColor = Tema.ColFondo;
            }
        }

        private void LlenarListaParadas()
        {
            llenandoLista = true;
            lvParadas.BeginUpdate();
            lvParadas.Items.Clear();
            foreach (Parada p in sistema.ListarParadas())
            {
                var item = new ListViewItem(p.Codigo) { Tag = p.Codigo };
                item.SubItems.Add(p.Nombre);
                item.SubItems.Add("Z" + sistema.NumeroZona(p.Codigo));
                lvParadas.Items.Add(item);
            }
            lvParadas.EndUpdate();
            llenandoLista = false;
            lblTituloLista.Text = "Paradas registradas (" + sistema.CantidadParadas + ")";
        }

        private void LlenarListaRutas()
        {
            lvRutas.BeginUpdate();
            lvRutas.Items.Clear();
            foreach (Ruta r in sistema.ListarRutas())
            {
                var item = new ListViewItem(r.Origen) { Tag = r };
                item.SubItems.Add(r.Destino);
                item.SubItems.Add(r.Tiempo.ToString("0.#", CultureInfo.InvariantCulture));
                item.SubItems.Add(r.Costo.ToString("0.00", CultureInfo.InvariantCulture));
                item.SubItems.Add(r.Unidireccional ? "1 sentido" : "ambos");
                lvRutas.Items.Add(item);
            }
            lvRutas.EndUpdate();
        }

        private void MostrarDetalle(string codigo)
        {
            Parada p = codigo == null ? null : sistema.BuscarParada(codigo);   // Tabla Hash
            if (p == null)
            {
                lblDetalleTitulo.Text = "Ninguna parada elegida";
                lblDetalleCuerpo.Text = "Escribe un nombre arriba, elige una de la lista o haz clic en una parada del mapa.";
                btnVerMapa.Enabled = false;
                btnEliminarParada.Enabled = false;
                return;
            }
            int rutas = sistema.Grados().Obtener(p.Codigo);
            lblDetalleTitulo.Text = p.Nombre;
            lblDetalleCuerpo.Text = "Código " + p.Codigo + Environment.NewLine +
                                    "Coordenadas " + p.Latitud.ToString("0.0000", CultureInfo.InvariantCulture) + ", " +
                                    p.Longitud.ToString("0.0000", CultureInfo.InvariantCulture) + Environment.NewLine +
                                    "Zona " + sistema.NumeroZona(p.Codigo) + ", " + rutas + (rutas == 1 ? " ruta" : " rutas");
            btnVerMapa.Enabled = true;
            btnEliminarParada.Enabled = true;
        }

        private void ActualizarZonaRuta()
        {
            if (lblZonaRuta == null) return;
            string o = rutaOrigen == null ? null : rutaOrigen.CodigoSeleccionado;
            string d = rutaDestino == null ? null : rutaDestino.CodigoSeleccionado;
            if (o == null || d == null)
            {
                lblZonaRuta.ForeColor = Tema.ColSuave;
                lblZonaRuta.Text = "Elige origen y destino para verificar en qué zona están antes de habilitar la ruta.";
                return;
            }
            if (o == d)
            {
                lblZonaRuta.ForeColor = Tema.ColCoral;
                lblZonaRuta.Text = "El origen y el destino son la misma parada.";
            }
            else if (sistema.MismaZona(o, d))
            {
                lblZonaRuta.ForeColor = Tema.ColSuave;
                lblZonaRuta.Text = "Ya están en la misma zona (zona " + sistema.NumeroZona(o) + "). La ruta será una alternativa más.";
            }
            else
            {
                lblZonaRuta.ForeColor = Tema.ColTeal;
                lblZonaRuta.Text = "Están en zonas distintas (zona " + sistema.NumeroZona(o) + " y zona " + sistema.NumeroZona(d) + "). Esta ruta las unirá.";
            }
        }

        private void Avisar(string mensaje, bool error = false)
        {
            toastTexto.Text = mensaje;
            toastBanda.BackColor = error ? Tema.ColCoral : Tema.ColTeal;
            toast.Left = Math.Max(8, (mapaVista.Width - toast.Width) / 2);
            toast.Top = 14;
            toast.Visible = true;
            toast.BringToFront();
            toastTimer.Stop();
            toastTimer.Start();
        }

        // ================================================================== acciones

        private void AlClicEnParada(string codigo)
        {
            seleccionActual = codigo;
            Parada p = sistema.BuscarParada(codigo);
            if (p == null) return;

            switch (paginaActual)
            {
                case 1: LlenarPar(rutaOrigen, rutaDestino, p); break;
                case 2: LlenarPar(calcOrigen, calcDestino, p); break;
                case 3: recInicio.Establecer(p); break;
                default: MostrarDetalle(codigo); break;
            }
        }

        /// <summary>Un clic llena el origen, el siguiente el destino, y el tercero empieza de nuevo.</summary>
        private static void LlenarPar(CajaSugerencias origen, CajaSugerencias destino, Parada p)
        {
            if (origen.CodigoSeleccionado == null) origen.Establecer(p);
            else if (destino.CodigoSeleccionado == null) destino.Establecer(p);
            else { destino.Limpiar(); origen.Establecer(p); }
        }

        private void RegistrarParada()
        {
            double lat, lon;
            if (!TryDouble(txtLat.Text, out lat) || !TryDouble(txtLon.Text, out lon))
            {
                Avisar("La latitud y la longitud deben ser números. Puedes elegirlas en el mapa.", true);
                return;
            }
            string error;
            string codigo = SistemaTransporte.NormalizarCodigo(txtCodigo.Text);
            if (sistema.RegistrarParada(txtCodigo.Text, txtNombre.Text, lat, lon, out error))
            {
                Avisar("Parada registrada: " + txtNombre.Text.Trim());
                txtCodigo.Clear(); txtNombre.Clear(); txtLat.Clear(); txtLon.Clear();
                seleccionActual = codigo;
                mapa.SeleccionarParada(codigo, false);
                MostrarDetalle(codigo);
            }
            else Avisar(error, true);
        }

        private void EliminarParadaSeleccionada()
        {
            if (seleccionActual == null) return;
            Parada p = sistema.BuscarParada(seleccionActual);
            if (p == null) return;
            var r = MessageBox.Show(this, "Se eliminará «" + p.Nombre + "» y todas las rutas que pasan por ella. ¿Continuar?",
                "Eliminar parada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            string error;
            if (sistema.EliminarParada(p.Codigo, out error)) { seleccionActual = null; Avisar("Parada eliminada."); }
            else Avisar(error, true);
        }

        private void RegistrarRuta()
        {
            string o = rutaOrigen.ObtenerCodigo(), d = rutaDestino.ObtenerCodigo();
            if (o == null || d == null) { Avisar("Elige el origen y el destino de la lista de sugerencias.", true); return; }
            double tiempo, costo;
            if (!TryDouble(txtTiempo.Text, out tiempo) || !TryDouble(txtCosto.Text, out costo))
            {
                Avisar("El tiempo y el costo deben ser números.", true);
                return;
            }
            string error;
            bool union;
            if (sistema.RegistrarRuta(o, d, tiempo, costo, chkUnSentido.Checked, out error, out union))
            {
                Avisar(union ? "Ruta habilitada. Une dos zonas que estaban separadas." : "Ruta habilitada dentro de la misma zona.");
                rutaOrigen.Limpiar(); rutaDestino.Limpiar(); txtTiempo.Clear(); txtCosto.Clear(); chkUnSentido.Checked = false;
            }
            else Avisar(error, true);
        }

        private void EliminarRutaSeleccionada()
        {
            if (lvRutas.SelectedItems.Count == 0) { Avisar("Elige una ruta de la lista.", true); return; }
            var r = (Ruta)lvRutas.SelectedItems[0].Tag;
            string error;
            if (sistema.EliminarRuta(r.Origen, r.Destino, out error)) Avisar("Ruta eliminada. Las zonas se recalcularon.");
            else Avisar(error, true);
        }

        private void CalcularRuta()
        {
            string o = calcOrigen.ObtenerCodigo(), d = calcDestino.ObtenerCodigo();
            if (o == null || d == null) { Avisar("Elige el origen y el destino de la lista de sugerencias.", true); return; }

            CriterioPeso criterio = rbTiempo.Checked ? CriterioPeso.Tiempo : CriterioPeso.Costo;
            ResultadoRuta res = sistema.CalcularRuta(o, d, criterio);

            var sb = new StringBuilder();
            foreach (string paso in res.Pasos) sb.AppendLine(paso);
            txtPasos.Text = sb.ToString();

            if (!res.Exito)
            {
                mapa.LimpiarResaltado();
                linea.Limpiar();
                lblResumen.Text = "";
                Avisar(res.Mensaje, true);
                return;
            }

            lblResumen.Text = res.TiempoTotal.ToString("0.#", CultureInfo.InvariantCulture) + " min,  Q " +
                              res.CostoTotal.ToString("0.00", CultureInfo.InvariantCulture);
            mapa.MostrarCamino(res.Camino);
            MostrarLineaRuta(res);
            Avisar("Ruta encontrada con " + res.Camino.Cantidad + " paradas.");
        }

        private void MostrarLineaRuta(ResultadoRuta res)
        {
            var items = new ItemLinea[res.Camino.Cantidad];
            double tiempo = 0, costo = 0;
            string anterior = null;
            int k = 0;
            foreach (string codigo in res.Camino)
            {
                if (anterior != null)
                {
                    Ruta tramo = sistema.ObtenerRuta(anterior, codigo);
                    if (tramo != null) { tiempo += tramo.Tiempo; costo += tramo.Costo; }
                }
                Parada p = sistema.BuscarParada(codigo);
                items[k] = new ItemLinea
                {
                    Codigo = codigo,
                    Titulo = p == null ? codigo : p.Nombre,
                    Etiqueta = k == 0 ? "Origen" : (k == items.Length - 1 ? "Destino" : "Parada " + (k + 1)),
                    Linea1 = k == 0 ? "Salida" : "+" + tiempo.ToString("0.#", CultureInfo.InvariantCulture) + " min",
                    Linea2 = "Q " + costo.ToString("0.00", CultureInfo.InvariantCulture),
                    Estado = k == 0 ? 1 : (k == items.Length - 1 ? 2 : 0)
                };
                anterior = codigo;
                k++;
            }

            string criterio = res.Criterio == CriterioPeso.Tiempo ? "Ruta más rápida" : "Ruta más económica";
            Kpi[] kpis =
            {
                new Kpi("Tiempo", res.TiempoTotal.ToString("0.#", CultureInfo.InvariantCulture) + " min"),
                new Kpi("Costo", "Q " + res.CostoTotal.ToString("0.00", CultureInfo.InvariantCulture)),
                new Kpi("Paradas", items.Length.ToString())
            };
            Parada po = sistema.BuscarParada(items[0].Codigo), pd = sistema.BuscarParada(items[items.Length - 1].Codigo);
            linea.Mostrar(criterio, (po == null ? "" : po.Nombre) + " hacia " + (pd == null ? "" : pd.Nombre), kpis, items);
        }

        private void EjecutarRecorrido(bool anchura)
        {
            string c = recInicio.ObtenerCodigo();
            if (c == null) { Avisar("Elige la parada de inicio de la lista de sugerencias.", true); return; }

            string error;
            ListaEnlazada<string> orden = anchura ? sistema.RecorridoBFS(c, out error) : sistema.RecorridoDFS(c, out error);
            string nombre = anchura ? "BFS (anchura)" : "DFS (profundidad)";
            if (orden == null)
            {
                Salida(nombre + "\n\n" + error);
                Avisar(error, true);
                return;
            }

            mapa.MostrarSecuencia(orden);
            var items = new ItemLinea[orden.Cantidad];
            var sb = new StringBuilder();
            sb.AppendLine("Recorrido " + nombre + " desde " + c + ": " + orden.Cantidad + " paradas alcanzadas");
            sb.AppendLine();
            int k = 0;
            foreach (string codigo in orden)
            {
                Parada p = sistema.BuscarParada(codigo);
                string n = p == null ? codigo : p.Nombre;
                items[k] = new ItemLinea
                {
                    Codigo = codigo,
                    Titulo = n,
                    Etiqueta = k == 0 ? "Inicio" : "Visita",
                    Linea1 = "Orden " + (k + 1),
                    Linea2 = "Zona " + sistema.NumeroZona(codigo),
                    Estado = k == 0 ? 1 : 0
                };
                sb.AppendLine((k + 1).ToString().PadLeft(3) + ". " + codigo + "  " + n);
                k++;
            }
            Salida(sb.ToString());
            linea.Mostrar("Recorrido " + nombre, "Desde " + c, new[] { new Kpi("Alcanzadas", orden.Cantidad.ToString()) }, items);
        }

        // ================================================================== archivos

        private static string CarpetaDatos { get { return Path.Combine(AppContext.BaseDirectory, "Datos"); } }

        private void CargaInicial()
        {
            string pp = Path.Combine(CarpetaDatos, "paradas.csv");
            string pr = Path.Combine(CarpetaDatos, "rutas.csv");
            if (!File.Exists(pp))
            {
                Avisar("No se encontraron los datos de ejemplo en " + CarpetaDatos + ". Usa Cargar paradas o registra una parada.", true);
                return;
            }
            ResultadoCarga a = sistema.CargarParadas(pp);
            ResultadoCarga b = File.Exists(pr) ? sistema.CargarRutas(pr) : null;
            int errores = a.Errores.Cantidad + (b == null ? 0 : b.Errores.Cantidad);
            Avisar(a.Cargados + " paradas y " + (b == null ? 0 : b.Cargados) + " rutas cargadas" +
                   (errores > 0 ? ". " + errores + " filas con errores, revisa Análisis." : "."), errores > 0);
            if (errores > 0) MostrarErrores(a, b);
        }

        private void CargarArchivo(bool paradas)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = paradas ? "Cargar paradas" : "Cargar rutas";
                dlg.Filter = "Datos (*.csv;*.txt)|*.csv;*.txt|Todos los archivos|*.*";
                if (Directory.Exists(CarpetaDatos)) dlg.InitialDirectory = CarpetaDatos;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                ResultadoCarga res = paradas ? sistema.CargarParadas(dlg.FileName) : sistema.CargarRutas(dlg.FileName);
                if (!res.ArchivoValido)
                {
                    MessageBox.Show(this, res.Errores.Cantidad > 0 ? res.Errores[0] : "No se pudo leer el archivo.",
                        "No se pudo cargar", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                string que = paradas ? " paradas" : " rutas";
                Avisar(res.Cargados + que + " cargadas" + (res.Errores.Cantidad > 0 ? ". " + res.Errores.Cantidad + " filas con errores, revisa Análisis." : "."),
                       res.Errores.Cantidad > 0);
                if (res.Errores.Cantidad > 0) MostrarErrores(res, null);
            }
        }

        private void MostrarErrores(ResultadoCarga a, ResultadoCarga b)
        {
            var sb = new StringBuilder();
            sb.AppendLine("FILAS QUE NO SE PUDIERON CARGAR");
            sb.AppendLine();
            foreach (string e in a.Errores) sb.AppendLine("  " + e);
            if (b != null) foreach (string e in b.Errores) sb.AppendLine("  " + e);
            Salida(sb.ToString());
        }

        private void GuardarDatos()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Elige la carpeta donde guardar paradas.csv y rutas.csv";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string error;
                if (sistema.GuardarDatos(Path.Combine(dlg.SelectedPath, "paradas.csv"), Path.Combine(dlg.SelectedPath, "rutas.csv"), out error))
                    Avisar("Datos guardados en " + dlg.SelectedPath);
                else
                    Avisar(error, true);
            }
        }
    }
}
