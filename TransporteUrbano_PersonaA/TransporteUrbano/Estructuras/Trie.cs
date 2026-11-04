using System;
using System.Globalization;
using System.Text;

namespace TransporteUrbano.Estructuras
{
    /// <summary>
    /// Nodo del Trie. Los hijos se guardan como lista enlazada ORDENADA por carácter
    /// (representación "primer hijo / hermano"), así no se necesita un arreglo de 256 posiciones
    /// ni un Dictionary, y el recorrido sale en orden alfabético.
    /// </summary>
    public class NodoTrie
    {
        public char Caracter;
        public NodoTrie PrimerHijo;
        public NodoTrie Hermano;
        public bool EsFin;                       // true si aquí termina un nombre de parada
        public string NombreOriginal;            // nombre tal como se registró (con tildes y mayúsculas)
        public ListaEnlazada<string> Codigos;    // códigos de las paradas con ese nombre

        public NodoTrie(char caracter) { Caracter = caracter; }
    }

    /// <summary>Una sugerencia del autocompletado: nombre para mostrar y el código para ir a la Tabla Hash.</summary>
    public class Sugerencia
    {
        public string Nombre { get; }
        public string Codigo { get; }
        public Sugerencia(string nombre, string codigo) { Nombre = nombre; Codigo = codigo; }
        public override string ToString() { return Nombre + " (" + Codigo + ")"; }
    }

    /// <summary>
    /// [PERSONA A] Trie (árbol de prefijos) propio para el autocompletado de nombres de parada.
    ///
    /// Se normaliza el texto (minúsculas, sin tildes, espacios simples) para que "cívico" y "Civico" coincidan.
    ///
    /// Complejidad (L = largo de la palabra, k = resultados devueltos):
    ///  - Insertar / Buscar / Eliminar: O(L * s), s = hijos por nodo (lista ordenada; en la práctica pequeño).
    ///  - Búsqueda por prefijo: O(L * s + nodos visitados en el subárbol), no depende de cuántas paradas hay
    ///    en total. Una lista con StartsWith recorre las n paradas: O(n * L).
    /// </summary>
    public class Trie
    {
        private readonly NodoTrie raiz = new NodoTrie('\0');
        private int nodos = 1;
        private int palabras;
        private int lineas, limiteLineas;

        public int CantidadNodos { get { return nodos; } }
        public int CantidadNombres { get { return palabras; } }

        /// <summary>Minúsculas, sin tildes ni diéresis, espacios múltiples colapsados.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";
            string descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            bool espacioPrevio = false;
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(c))
                {
                    if (!espacioPrevio) sb.Append(' ');
                    espacioPrevio = true;
                }
                else
                {
                    sb.Append(c);
                    espacioPrevio = false;
                }
            }
            return sb.ToString();
        }

        private static NodoTrie HijoConCaracter(NodoTrie padre, char c)
        {
            NodoTrie h = padre.PrimerHijo;
            while (h != null && h.Caracter < c) h = h.Hermano;
            return (h != null && h.Caracter == c) ? h : null;
        }

        private NodoTrie ObtenerOCrearHijo(NodoTrie padre, char c)
        {
            NodoTrie anterior = null;
            NodoTrie actual = padre.PrimerHijo;
            while (actual != null && actual.Caracter < c) { anterior = actual; actual = actual.Hermano; }
            if (actual != null && actual.Caracter == c) return actual;

            var nuevo = new NodoTrie(c);
            nuevo.Hermano = actual;
            if (anterior == null) padre.PrimerHijo = nuevo;
            else anterior.Hermano = nuevo;
            nodos++;
            return nuevo;
        }

        public void Insertar(string nombre, string codigo)
        {
            string clave = Normalizar(nombre);
            if (clave.Length == 0) return;

            NodoTrie nodo = raiz;
            for (int i = 0; i < clave.Length; i++) nodo = ObtenerOCrearHijo(nodo, clave[i]);

            if (!nodo.EsFin)
            {
                nodo.EsFin = true;
                nodo.Codigos = new ListaEnlazada<string>();
                palabras++;
            }
            nodo.NombreOriginal = nombre.Trim();
            if (!nodo.Codigos.Existe(c => c == codigo)) nodo.Codigos.AgregarFinal(codigo);
        }

        /// <summary>Busca un nombre exacto. Devuelve los códigos asociados o null si no existe.</summary>
        public ListaEnlazada<string> BuscarExacto(string nombre)
        {
            NodoTrie nodo = BuscarNodo(Normalizar(nombre));
            return (nodo != null && nodo.EsFin) ? nodo.Codigos : null;
        }

        private NodoTrie BuscarNodo(string claveNormalizada)
        {
            NodoTrie nodo = raiz;
            for (int i = 0; i < claveNormalizada.Length; i++)
            {
                nodo = HijoConCaracter(nodo, claveNormalizada[i]);
                if (nodo == null) return null;
            }
            return nodo;
        }

        /// <summary>
        /// Elimina el código asociado a un nombre. Si el nombre se queda sin códigos se desmarca y se
        /// podan los nodos que quedaron sin hijos ni palabra (para no dejar basura en el árbol).
        /// </summary>
        public bool Eliminar(string nombre, string codigo)
        {
            string clave = Normalizar(nombre);
            if (clave.Length == 0) return false;

            var camino = new NodoTrie[clave.Length + 1];
            camino[0] = raiz;
            for (int i = 0; i < clave.Length; i++)
            {
                NodoTrie h = HijoConCaracter(camino[i], clave[i]);
                if (h == null) return false;
                camino[i + 1] = h;
            }

            NodoTrie fin = camino[clave.Length];
            if (!fin.EsFin) return false;
            if (!fin.Codigos.Eliminar(c => c == codigo)) return false;

            if (fin.Codigos.Cantidad == 0)
            {
                fin.EsFin = false;
                fin.NombreOriginal = null;
                fin.Codigos = null;
                palabras--;
            }

            // Poda hacia arriba: se quitan los nodos que ya no sirven.
            for (int i = clave.Length; i >= 1; i--)
            {
                NodoTrie n = camino[i];
                if (n.EsFin || n.PrimerHijo != null) break;
                DesenlazarHijo(camino[i - 1], n);
                nodos--;
            }
            return true;
        }

        private static void DesenlazarHijo(NodoTrie padre, NodoTrie hijo)
        {
            if (padre.PrimerHijo == hijo) { padre.PrimerHijo = hijo.Hermano; return; }
            NodoTrie h = padre.PrimerHijo;
            while (h != null && h.Hermano != hijo) h = h.Hermano;
            if (h != null) h.Hermano = hijo.Hermano;
        }

        /// <summary>Autocompletado: nombres que empiezan con el prefijo, en orden alfabético, máximo "maximo".</summary>
        public ListaEnlazada<Sugerencia> BuscarPorPrefijo(string prefijo, int maximo)
        {
            var resultado = new ListaEnlazada<Sugerencia>();
            NodoTrie inicio = BuscarNodo(Normalizar(prefijo));
            if (inicio != null) Recolectar(inicio, resultado, maximo);
            return resultado;
        }

        private void Recolectar(NodoTrie nodo, ListaEnlazada<Sugerencia> resultado, int maximo)
        {
            if (nodo.EsFin)
            {
                foreach (string codigo in nodo.Codigos)
                {
                    if (resultado.Cantidad >= maximo) return;
                    resultado.AgregarFinal(new Sugerencia(nodo.NombreOriginal, codigo));
                }
            }
            for (NodoTrie h = nodo.PrimerHijo; h != null && resultado.Cantidad < maximo; h = h.Hermano)
                Recolectar(h, resultado, maximo);
        }

        /// <summary>Recorrido completo del Trie: todos los nombres en orden alfabético.</summary>
        public ListaEnlazada<Sugerencia> Recorrer()
        {
            return BuscarPorPrefijo("", int.MaxValue);
        }

        /// <summary>Dibuja el árbol como texto indentado (los espacios se muestran como "_").</summary>
        public string ImprimirArbol(int maxLineas)
        {
            var sb = new StringBuilder();
            lineas = 0;
            limiteLineas = maxLineas;
            sb.AppendLine("(raíz)");
            ImprimirRec(raiz, 1, sb);
            if (lineas >= limiteLineas) sb.AppendLine("... (salida recortada a " + maxLineas + " líneas)");
            return sb.ToString();
        }

        private void ImprimirRec(NodoTrie padre, int nivel, StringBuilder sb)
        {
            for (NodoTrie h = padre.PrimerHijo; h != null; h = h.Hermano)
            {
                if (lineas >= limiteLineas) return;
                sb.Append(' ', nivel * 2).Append(h.Caracter == ' ' ? '_' : h.Caracter);
                if (h.EsFin)
                {
                    sb.Append("   <- ").Append(h.NombreOriginal).Append(" [");
                    bool primero = true;
                    foreach (string c in h.Codigos)
                    {
                        if (!primero) sb.Append(',');
                        sb.Append(c);
                        primero = false;
                    }
                    sb.Append(']');
                }
                sb.AppendLine();
                lineas++;
                ImprimirRec(h, nivel + 1, sb);
            }
        }
    }
}
