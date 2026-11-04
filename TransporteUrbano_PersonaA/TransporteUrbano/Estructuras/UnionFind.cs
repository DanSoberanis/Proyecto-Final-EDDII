namespace TransporteUrbano.Estructuras
{
    /// <summary>
    /// [PERSONA A] Union-Find (conjuntos disjuntos) con compresión de camino y unión por rango.
    ///
    /// Uso en el proyecto: cada conjunto es una "zona conectada" de la red. Al registrar una ruta se hace
    /// Union(origen, destino); antes de ejecutar Dijkstra se pregunta MismaZona(a, b) en casi O(1) para
    /// descartar rápido los pares imposibles, en vez de recorrer el grafo con BFS/DFS (O(V + E)).
    ///
    /// Los códigos de parada (string) se traducen a índices con una TablaHash propia.
    ///
    /// Complejidad: Find/Union O(α(n)) amortizado (inversa de Ackermann, prácticamente constante).
    /// Limitación: no soporta eliminar aristas. SistemaTransporte reconstruye la estructura cuando se borra
    /// una ruta o una parada (O(n + m α(n))).
    /// Nota: las rutas de un solo sentido cuentan como conexión "débil": dos paradas en la misma zona pueden
    /// no tener camino dirigido entre ellas, y en ese caso Dijkstra informa que no hay ruta.
    /// </summary>
    public class UnionFind
    {
        private readonly TablaHash<int> indices = new TablaHash<int>();
        private string[] codigos = new string[16];
        private int[] padre = new int[16];
        private int[] rango = new int[16];
        private int cantidad;
        private int zonas;

        public int Cantidad { get { return cantidad; } }
        public int CantidadZonas { get { return zonas; } }

        public bool Contiene(string codigo) { return indices.Contiene(codigo); }

        /// <summary>MakeSet: crea una zona nueva con un solo elemento.</summary>
        public bool Agregar(string codigo)
        {
            if (indices.Contiene(codigo)) return false;
            if (cantidad == padre.Length) Crecer();
            indices.Insertar(codigo, cantidad);
            codigos[cantidad] = codigo;
            padre[cantidad] = cantidad;
            rango[cantidad] = 0;
            cantidad++;
            zonas++;
            return true;
        }

        private void Crecer()
        {
            int nuevo = padre.Length * 2;
            var c = new string[nuevo];
            var p = new int[nuevo];
            var r = new int[nuevo];
            for (int i = 0; i < cantidad; i++) { c[i] = codigos[i]; p[i] = padre[i]; r[i] = rango[i]; }
            codigos = c; padre = p; rango = r;
        }

        /// <summary>Find con compresión de camino (dos pasadas, iterativo para no desbordar la pila).</summary>
        private int Raiz(int i)
        {
            int r = i;
            while (padre[r] != r) r = padre[r];
            while (padre[i] != r)
            {
                int siguiente = padre[i];
                padre[i] = r;     // compresión: cada nodo del camino apunta directo a la raíz
                i = siguiente;
            }
            return r;
        }

        /// <summary>Devuelve el código representante de la zona o null si el elemento no existe.</summary>
        public string Find(string codigo)
        {
            int i;
            if (!indices.Buscar(codigo, out i)) return null;
            return codigos[Raiz(i)];
        }

        /// <summary>Union por rango. Devuelve true si realmente unió dos zonas distintas.</summary>
        public bool Union(string a, string b)
        {
            int ia, ib;
            if (!indices.Buscar(a, out ia) || !indices.Buscar(b, out ib)) return false;
            int ra = Raiz(ia), rb = Raiz(ib);
            if (ra == rb) return false;

            if (rango[ra] < rango[rb]) padre[ra] = rb;
            else if (rango[ra] > rango[rb]) padre[rb] = ra;
            else { padre[rb] = ra; rango[ra]++; }
            zonas--;
            return true;
        }

        public bool MismaZona(string a, string b)
        {
            int ia, ib;
            if (!indices.Buscar(a, out ia) || !indices.Buscar(b, out ib)) return false;
            return Raiz(ia) == Raiz(ib);
        }

        public int TamanoZona(string codigo)
        {
            int i;
            if (!indices.Buscar(codigo, out i)) return 0;
            int r = Raiz(i), total = 0;
            for (int k = 0; k < cantidad; k++) if (Raiz(k) == r) total++;
            return total;
        }

        public ListaEnlazada<string> MiembrosDeZona(string codigo)
        {
            var lista = new ListaEnlazada<string>();
            int i;
            if (!indices.Buscar(codigo, out i)) return lista;
            int r = Raiz(i);
            for (int k = 0; k < cantidad; k++) if (Raiz(k) == r) lista.AgregarFinal(codigos[k]);
            return lista;
        }
    }
}
