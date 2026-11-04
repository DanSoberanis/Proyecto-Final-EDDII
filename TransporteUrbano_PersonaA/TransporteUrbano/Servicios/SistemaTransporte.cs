using System;
using System.Text;
using TransporteUrbano.Estructuras;
using TransporteUrbano.Models;

namespace TransporteUrbano.Servicios
{
    /// <summary>
    /// Orquesta las cuatro estructuras en un mismo flujo (requisito 2 del proyecto):
    ///
    ///   Trie        -> sugiere nombres mientras se escribe              (SugerirParadas)
    ///   Tabla Hash  -> localiza el registro completo por su código      (BuscarParada)
    ///   Union-Find  -> valida que ambas paradas estén en la misma zona  (MismaZona)
    ///   Grafo       -> calcula la ruta con Dijkstra / recorre BFS y DFS (IGrafoTransporte, Persona B)
    ///
    /// La interfaz gráfica solo habla con esta clase. Si el Grafo todavía no existe, todo lo demás funciona.
    /// </summary>
    public class SistemaTransporte
    {
        private readonly TablaHash<Parada> paradas = new TablaHash<Parada>();     // código -> Parada
        private readonly TablaHash<Ruta> aristas = new TablaHash<Ruta>();         // "ORIGEN>DESTINO" -> Ruta
        private readonly Trie trie = new Trie();                                  // nombre -> códigos
        private UnionFind zonas = new UnionFind();                                // zonas conectadas
        private readonly ListaEnlazada<Ruta> rutas = new ListaEnlazada<Ruta>();   // catálogo de rutas
        private readonly IGrafoTransporte grafo;

        private TablaHash<int> cacheZonas;    // raíz de zona -> número de zona (1, 2, 3...)
        private TablaHash<int> cacheGrados;   // código -> cantidad de rutas que tocan la parada
        private bool suspendido;

        /// <summary>Se dispara cada vez que cambian las paradas o las rutas (la interfaz se refresca).</summary>
        public event Action DatosCambiaron;

        public SistemaTransporte(IGrafoTransporte grafo)
        {
            this.grafo = grafo;
        }

        public IGrafoTransporte Grafo { get { return grafo; } }
        public bool GrafoDisponible { get { return grafo.Disponible; } }
        public int CantidadParadas { get { return paradas.Cantidad; } }
        public int CantidadRutas { get { return rutas.Cantidad; } }
        public int CantidadZonas { get { return zonas.CantidadZonas; } }

        private static string Clave(string origen, string destino) { return origen + ">" + destino; }
        public static string NormalizarCodigo(string codigo) { return (codigo ?? "").Trim().ToUpperInvariant(); }

        private void Cambio()
        {
            cacheZonas = null;
            cacheGrados = null;
            if (!suspendido && DatosCambiaron != null) DatosCambiaron();
        }

        // ------------------------------------------------------------------ PARADAS

        public bool RegistrarParada(string codigo, string nombre, double latitud, double longitud, out string error)
        {
            error = null;
            string c = NormalizarCodigo(codigo);
            string n = (nombre ?? "").Trim();

            if (c.Length == 0) { error = "El código es obligatorio."; return false; }
            if (c.IndexOfAny(new[] { ',', ';', '>', ' ', '\t' }) >= 0) { error = "El código no puede tener espacios, comas ni punto y coma."; return false; }
            if (n.Length == 0) { error = "El nombre es obligatorio."; return false; }
            if (n.IndexOfAny(new[] { ',', ';' }) >= 0) { error = "El nombre no puede tener comas ni punto y coma."; return false; }
            if (latitud < -90 || latitud > 90) { error = "La latitud debe estar entre -90 y 90."; return false; }
            if (longitud < -180 || longitud > 180) { error = "La longitud debe estar entre -180 y 180."; return false; }
            if (paradas.Contiene(c)) { error = "Ya existe una parada con el código " + c + "."; return false; }

            var p = new Parada(c, n, latitud, longitud);
            paradas.Insertar(c, p);      // Tabla Hash
            trie.Insertar(n, c);         // Trie
            zonas.Agregar(c);            // Union-Find: nace como su propia zona
            grafo.AgregarVertice(c);     // Grafo
            Cambio();
            return true;
        }

        public bool EliminarParada(string codigo, out string error)
        {
            error = null;
            string c = NormalizarCodigo(codigo);
            Parada p = paradas.Obtener(c);
            if (p == null) { error = "La parada " + c + " no existe."; return false; }

            var incidentes = new ListaEnlazada<Ruta>();
            foreach (Ruta r in rutas)
                if (r.Origen == c || r.Destino == c) incidentes.AgregarFinal(r);
            foreach (Ruta r in incidentes) QuitarRutaInterna(r);

            paradas.Eliminar(c);
            trie.Eliminar(p.Nombre, c);
            grafo.EliminarVertice(c);
            ReconstruirZonas();          // Union-Find no puede "des-unir", se reconstruye
            Cambio();
            return true;
        }

        /// <summary>Búsqueda por código en la Tabla Hash: O(1) promedio.</summary>
        public Parada BuscarParada(string codigo)
        {
            return paradas.Obtener(NormalizarCodigo(codigo));
        }

        /// <summary>Autocompletado con el Trie (máximo 8 sugerencias).</summary>
        public ListaEnlazada<Sugerencia> SugerirParadas(string prefijo)
        {
            return trie.BuscarPorPrefijo(prefijo, 8);
        }

        /// <summary>Paradas ordenadas por código (ordenamiento por inserción sobre un arreglo auxiliar).</summary>
        public ListaEnlazada<Parada> ListarParadas()
        {
            Parada[] arr = paradas.ObtenerValores().ToArray();
            for (int i = 1; i < arr.Length; i++)
            {
                Parada clave = arr[i];
                int j = i - 1;
                while (j >= 0 && string.CompareOrdinal(arr[j].Codigo, clave.Codigo) > 0) { arr[j + 1] = arr[j]; j--; }
                arr[j + 1] = clave;
            }
            var lista = new ListaEnlazada<Parada>();
            foreach (Parada p in arr) lista.AgregarFinal(p);
            return lista;
        }

        // ------------------------------------------------------------------ RUTAS

        /// <summary>
        /// Habilita una ruta entre dos paradas. "unioZonas" indica el resultado de la verificación del Union-Find:
        /// true = estaban en zonas distintas y la nueva ruta las une; false = ya estaban en la misma zona.
        /// </summary>
        public bool RegistrarRuta(string origen, string destino, double tiempo, double costo, bool unidireccional,
                                  out string error, out bool unioZonas)
        {
            error = null;
            unioZonas = false;
            string o = NormalizarCodigo(origen), d = NormalizarCodigo(destino);

            if (!paradas.Contiene(o)) { error = "La parada de origen (" + o + ") no existe."; return false; }
            if (!paradas.Contiene(d)) { error = "La parada de destino (" + d + ") no existe."; return false; }
            if (o == d) { error = "El origen y el destino deben ser paradas distintas."; return false; }
            if (tiempo <= 0) { error = "El tiempo debe ser mayor que cero."; return false; }
            if (costo < 0) { error = "El costo no puede ser negativo."; return false; }
            if (aristas.Contiene(Clave(o, d)) || aristas.Contiene(Clave(d, o)))
            {
                error = "Ya existe una ruta entre " + o + " y " + d + ".";
                return false;
            }

            unioZonas = !zonas.MismaZona(o, d);    // verificación previa con Union-Find

            var r = new Ruta(o, d, tiempo, costo, unidireccional);
            rutas.AgregarFinal(r);
            aristas.Insertar(Clave(o, d), r);
            if (!unidireccional) aristas.Insertar(Clave(d, o), r);
            grafo.AgregarArista(r);
            zonas.Union(o, d);
            Cambio();
            return true;
        }

        public bool EliminarRuta(string origen, string destino, out string error)
        {
            error = null;
            Ruta r = aristas.Obtener(Clave(NormalizarCodigo(origen), NormalizarCodigo(destino)));
            if (r == null) { error = "No existe esa ruta."; return false; }
            QuitarRutaInterna(r);
            ReconstruirZonas();
            Cambio();
            return true;
        }

        private void QuitarRutaInterna(Ruta r)
        {
            rutas.Eliminar(x => ReferenceEquals(x, r));
            aristas.Eliminar(Clave(r.Origen, r.Destino));
            if (!r.Unidireccional) aristas.Eliminar(Clave(r.Destino, r.Origen));
            grafo.EliminarArista(r);
        }

        private void ReconstruirZonas()
        {
            zonas = new UnionFind();
            foreach (Parada p in paradas.ObtenerValores()) zonas.Agregar(p.Codigo);
            foreach (Ruta r in rutas) zonas.Union(r.Origen, r.Destino);
        }

        /// <summary>Ruta que permite ir de a hacia b (null si no existe ese sentido). O(1) con la Tabla Hash.</summary>
        public Ruta ObtenerRuta(string a, string b) { return aristas.Obtener(Clave(a, b)); }

        public ListaEnlazada<Ruta> ListarRutas()
        {
            var copia = new ListaEnlazada<Ruta>();
            foreach (Ruta r in rutas) copia.AgregarFinal(r);
            return copia;
        }

        // ------------------------------------------------------------------ ZONAS (Union-Find)

        public bool MismaZona(string a, string b)
        {
            return zonas.MismaZona(NormalizarCodigo(a), NormalizarCodigo(b));
        }

        /// <summary>
        /// Numera las zonas 1, 2, 3... ordenadas por el menor código de parada que contiene cada una,
        /// así la numeración es estable aunque cambie el representante interno del Union-Find.
        /// </summary>
        public TablaHash<int> MapaZonas()
        {
            if (cacheZonas != null) return cacheZonas;

            var menor = new TablaHash<string>();
            foreach (Parada p in paradas.ObtenerValores())
            {
                string raiz = zonas.Find(p.Codigo);
                string actual = menor.Obtener(raiz);
                if (actual == null || string.CompareOrdinal(p.Codigo, actual) < 0) menor.Establecer(raiz, p.Codigo);
            }

            string[] raices = menor.ObtenerClaves().ToArray();
            string[] minimos = new string[raices.Length];
            for (int i = 0; i < raices.Length; i++) minimos[i] = menor.Obtener(raices[i]);

            for (int i = 1; i < raices.Length; i++)   // ordenamiento por inserción
            {
                string r = raices[i], m = minimos[i];
                int j = i - 1;
                while (j >= 0 && string.CompareOrdinal(minimos[j], m) > 0) { raices[j + 1] = raices[j]; minimos[j + 1] = minimos[j]; j--; }
                raices[j + 1] = r; minimos[j + 1] = m;
            }

            var mapa = new TablaHash<int>();
            for (int i = 0; i < raices.Length; i++) mapa.Insertar(raices[i], i + 1);
            cacheZonas = mapa;
            return mapa;
        }

        public int NumeroZona(string codigo)
        {
            string raiz = zonas.Find(NormalizarCodigo(codigo));
            return raiz == null ? 0 : MapaZonas().Obtener(raiz);
        }

        public TablaHash<int> Grados()
        {
            if (cacheGrados != null) return cacheGrados;
            var g = new TablaHash<int>();
            foreach (Ruta r in rutas)
            {
                g.Establecer(r.Origen, g.Obtener(r.Origen) + 1);
                g.Establecer(r.Destino, g.Obtener(r.Destino) + 1);
            }
            cacheGrados = g;
            return g;
        }

        // ------------------------------------------------------------------ CÁLCULO DE RUTA (flujo integrado)

        public ResultadoRuta CalcularRuta(string origen, string destino, CriterioPeso criterio)
        {
            var res = new ResultadoRuta { Criterio = criterio };
            string o = NormalizarCodigo(origen), d = NormalizarCodigo(destino);
            string nombreCriterio = criterio == CriterioPeso.Tiempo ? "tiempo" : "costo";

            // 1) Tabla Hash: registro completo de cada parada
            Parada po = paradas.Obtener(o), pd = paradas.Obtener(d);
            if (po == null || pd == null)
            {
                res.Mensaje = "Elige dos paradas que existan en la red.";
                res.Pasos.AgregarFinal("✗ Tabla Hash: no se encontró " + (po == null ? o : d));
                return res;
            }
            res.Pasos.AgregarFinal("✓ Tabla Hash: localizó «" + po.Nombre + "» y «" + pd.Nombre + "» en O(1)");

            if (o == d)
            {
                res.Mensaje = "El origen y el destino son la misma parada.";
                res.Pasos.AgregarFinal("✗ El origen y el destino coinciden");
                return res;
            }

            // 2) Union-Find: ¿pertenecen a la misma zona conectada?
            if (!zonas.MismaZona(o, d))
            {
                res.Mensaje = "Las paradas están en zonas distintas (zona " + NumeroZona(o) + " y zona " + NumeroZona(d) +
                              "): no hay ninguna ruta que las conecte. Habilita una ruta nueva entre las dos zonas.";
                res.Pasos.AgregarFinal("✗ Union-Find: zona " + NumeroZona(o) + " y zona " + NumeroZona(d) + ", no están conectadas");
                res.Pasos.AgregarFinal("   Dijkstra no se ejecutó");
                return res;
            }
            res.Pasos.AgregarFinal("✓ Union-Find: ambas paradas están en la zona " + NumeroZona(o));

            // 3) Grafo: Dijkstra
            if (!grafo.Disponible)
            {
                res.Mensaje = GrafoNoDisponible.Aviso;
                res.Pasos.AgregarFinal("… Grafo: pendiente (Persona B)");
                return res;
            }
            ResultadoRuta rg = grafo.Dijkstra(o, d, criterio);
            if (rg == null || !rg.Exito)
            {
                res.Mensaje = rg == null ? "El grafo no devolvió resultado." : rg.Mensaje;
                res.Pasos.AgregarFinal("✗ Grafo: " + res.Mensaje);
                return res;
            }

            // Totales de tiempo y costo recorriendo el camino (cada tramo se busca en O(1) en la Tabla Hash)
            string anterior = null;
            foreach (string codigo in rg.Camino)
            {
                if (anterior != null)
                {
                    Ruta tramo = aristas.Obtener(Clave(anterior, codigo));
                    if (tramo == null)
                    {
                        res.Mensaje = "El grafo devolvió un tramo que no existe: " + anterior + " -> " + codigo + ".";
                        res.Pasos.AgregarFinal("✗ Grafo: " + res.Mensaje);
                        return res;
                    }
                    res.TiempoTotal += tramo.Tiempo;
                    res.CostoTotal += tramo.Costo;
                }
                anterior = codigo;
            }

            res.Exito = true;
            res.Camino = rg.Camino;
            res.PesoTotal = rg.PesoTotal;
            res.Mensaje = "Ruta encontrada.";
            res.Pasos.AgregarFinal("✓ Grafo: Dijkstra por " + nombreCriterio + " encontró un camino de " + rg.Camino.Cantidad + " paradas");
            return res;
        }

        public ListaEnlazada<string> RecorridoBFS(string inicio, out string error)
        {
            return Recorrer(inicio, true, out error);
        }

        public ListaEnlazada<string> RecorridoDFS(string inicio, out string error)
        {
            return Recorrer(inicio, false, out error);
        }

        private ListaEnlazada<string> Recorrer(string inicio, bool anchura, out string error)
        {
            error = null;
            string c = NormalizarCodigo(inicio);
            if (!paradas.Contiene(c)) { error = "La parada de inicio no existe."; return null; }
            if (!grafo.Disponible) { error = GrafoNoDisponible.Aviso; return null; }
            return anchura ? grafo.BFS(c) : grafo.DFS(c);
        }

        // ------------------------------------------------------------------ ARCHIVOS

        public ResultadoCarga CargarParadas(string archivo)
        {
            var res = new ResultadoCarga();
            var leidas = new ListaEnlazada<Parada>();
            res.ArchivoValido = CargadorArchivos.LeerParadas(archivo, leidas, res.Errores);
            if (!res.ArchivoValido) return res;

            suspendido = true;
            try
            {
                foreach (Parada p in leidas)
                {
                    res.Leidos++;
                    string error;
                    if (RegistrarParada(p.Codigo, p.Nombre, p.Latitud, p.Longitud, out error)) res.Cargados++;
                    else res.Errores.AgregarFinal(p.Codigo + ": " + error);
                }
            }
            finally { suspendido = false; Cambio(); }
            return res;
        }

        public ResultadoCarga CargarRutas(string archivo)
        {
            var res = new ResultadoCarga();
            var leidas = new ListaEnlazada<Ruta>();
            res.ArchivoValido = CargadorArchivos.LeerRutas(archivo, leidas, res.Errores);
            if (!res.ArchivoValido) return res;

            suspendido = true;
            try
            {
                foreach (Ruta r in leidas)
                {
                    res.Leidos++;
                    string error;
                    bool union;
                    if (RegistrarRuta(r.Origen, r.Destino, r.Tiempo, r.Costo, r.Unidireccional, out error, out union)) res.Cargados++;
                    else res.Errores.AgregarFinal(r.Origen + " -> " + r.Destino + ": " + error);
                }
            }
            finally { suspendido = false; Cambio(); }
            return res;
        }

        public bool GuardarDatos(string archivoParadas, string archivoRutas, out string error)
        {
            error = null;
            try
            {
                CargadorArchivos.GuardarParadas(archivoParadas, ListarParadas());
                CargadorArchivos.GuardarRutas(archivoRutas, ListarRutas());
                return true;
            }
            catch (Exception ex)
            {
                error = "No se pudo guardar: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ REPORTES (para la toma de decisiones)

        public string ReporteZonas()
        {
            if (paradas.Cantidad == 0) return "No hay paradas registradas.";

            TablaHash<int> mapa = MapaZonas();
            var porZona = new TablaHash<ListaEnlazada<Parada>>();
            foreach (Parada p in ListarParadas())
            {
                string raiz = zonas.Find(p.Codigo);
                ListaEnlazada<Parada> lista = porZona.Obtener(raiz);
                if (lista == null) { lista = new ListaEnlazada<Parada>(); porZona.Insertar(raiz, lista); }
                lista.AgregarFinal(p);
            }

            int k = zonas.CantidadZonas;
            string[] raicePorNumero = new string[k + 1];
            foreach (string raiz in porZona.ObtenerClaves()) raicePorNumero[mapa.Obtener(raiz)] = raiz;

            var sb = new StringBuilder();
            sb.AppendLine("ZONAS CONECTADAS (Union-Find): " + k);
            sb.AppendLine();
            for (int n = 1; n <= k; n++)
            {
                ListaEnlazada<Parada> miembros = porZona.Obtener(raicePorNumero[n]);
                sb.AppendLine("Zona " + n + ": " + miembros.Cantidad + (miembros.Cantidad == 1 ? " parada" : " paradas"));
                bool primero = true;
                foreach (Parada p in miembros)
                {
                    sb.Append(primero ? "   " : ", ").Append(p.Nombre);
                    primero = false;
                }
                sb.AppendLine().AppendLine();
            }
            if (k > 1) sb.AppendLine("Para unir dos zonas basta habilitar una ruta entre una parada de cada una.");
            return sb.ToString();
        }

        public string ReporteMasConectadas(int top)
        {
            if (paradas.Cantidad == 0) return "No hay paradas registradas.";
            TablaHash<int> grados = Grados();
            Parada[] arr = ListarParadas().ToArray();

            int limite = Math.Min(top, arr.Length);
            for (int t = 0; t < limite; t++)   // selección parcial: las "top" con más rutas
            {
                int mejor = t;
                for (int i = t + 1; i < arr.Length; i++)
                    if (grados.Obtener(arr[i].Codigo) > grados.Obtener(arr[mejor].Codigo)) mejor = i;
                Parada tmp = arr[t]; arr[t] = arr[mejor]; arr[mejor] = tmp;
            }

            var sb = new StringBuilder();
            sb.AppendLine("PARADAS MÁS CONECTADAS (centros de transbordo)");
            sb.AppendLine();
            for (int t = 0; t < limite; t++)
                sb.AppendLine((t + 1) + ". " + arr[t].Nombre + " (" + arr[t].Codigo + "): " + grados.Obtener(arr[t].Codigo) + " rutas, zona " + NumeroZona(arr[t].Codigo));
            return sb.ToString();
        }

        public string ResumenEstructuras()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ESTADO DE LAS ESTRUCTURAS");
            sb.AppendLine();
            sb.AppendLine("Tabla Hash de paradas (clave: código)");
            sb.AppendLine("   elementos " + paradas.Cantidad + ", cubetas " + paradas.Capacidad + ", factor de carga " + paradas.FactorCarga.ToString("0.00"));
            sb.AppendLine("   cubetas ocupadas " + paradas.CubetasOcupadas + ", colisiones " + (paradas.Cantidad - paradas.CubetasOcupadas) + ", cadena más larga " + paradas.CadenaMasLarga);
            sb.AppendLine();
            sb.AppendLine("Tabla Hash de tramos (clave: ORIGEN>DESTINO)");
            sb.AppendLine("   elementos " + aristas.Cantidad + ", cubetas " + aristas.Capacidad + ", factor de carga " + aristas.FactorCarga.ToString("0.00"));
            sb.AppendLine();
            sb.AppendLine("Trie de nombres");
            sb.AppendLine("   nombres " + trie.CantidadNombres + ", nodos " + trie.CantidadNodos);
            sb.AppendLine();
            sb.AppendLine("Union-Find");
            sb.AppendLine("   elementos " + zonas.Cantidad + ", zonas " + zonas.CantidadZonas);
            sb.AppendLine();
            sb.AppendLine("Grafo");
            sb.AppendLine("   " + grafo.Nombre + (grafo.Disponible ? " (conectado)" : " (no conectado)"));
            sb.AppendLine("   rutas registradas " + rutas.Cantidad);
            return sb.ToString();
        }

        public string ImprimirTablaHash() { return paradas.Imprimir(); }
        public string ImprimirTrie() { return trie.ImprimirArbol(160); }
        public string ImprimirGrafo() { return grafo.Imprimir(); }
    }
}
