using System;
using System.Text;

namespace TransporteUrbano.Estructuras
{
    /// <summary>Nodo de una cadena (encadenamiento separado) de la tabla hash.</summary>
    public class NodoHash<TValor>
    {
        public string Clave;
        public TValor Valor;
        public NodoHash<TValor> Siguiente;
    }

    /// <summary>
    /// [PERSONA A] Tabla Hash propia con encadenamiento separado y rehashing automático.
    ///
    /// Uso en el proyecto: localizar una parada por su código en O(1) promedio.
    ///
    /// Decisiones de diseño (para la defensa):
    ///  - Función hash: variante djb2 (h = h*33 XOR c), reducida con módulo a un tamaño primo.
    ///  - Colisiones: encadenamiento (lista enlazada por cubeta). Nunca se "llena" y borrar es simple.
    ///  - Factor de carga máximo 0.75: al superarlo se pasa al siguiente primo (aprox. el doble)
    ///    y se reubican los nodos (rehashing). Así las cadenas se mantienen cortas.
    ///
    /// Complejidad: Insertar/Buscar/Eliminar O(1) promedio, O(n) peor caso (todo en una cubeta).
    /// Insertar es O(1) amortizado por el rehashing. Recorrer/Imprimir O(capacidad + n).
    /// </summary>
    public class TablaHash<TValor>
    {
        private static readonly int[] Tamanos = { 17, 37, 79, 163, 331, 673, 1361, 2729, 5471, 10949, 21911, 43853, 87719 };
        private const double FactorCargaMaximo = 0.75;

        private NodoHash<TValor>[] cubetas;
        private int indiceTamano;
        private int cantidad;

        public TablaHash()
        {
            cubetas = new NodoHash<TValor>[Tamanos[0]];
        }

        public int Cantidad { get { return cantidad; } }
        public int Capacidad { get { return cubetas.Length; } }
        public double FactorCarga { get { return (double)cantidad / cubetas.Length; } }

        private static int Hash(string clave, int tamano)
        {
            unchecked
            {
                uint h = 5381;
                for (int i = 0; i < clave.Length; i++) h = (h * 33) ^ clave[i];
                return (int)(h % (uint)tamano);
            }
        }

        private NodoHash<TValor> BuscarNodo(string clave)
        {
            int i = Hash(clave, cubetas.Length);
            for (NodoHash<TValor> n = cubetas[i]; n != null; n = n.Siguiente)
                if (n.Clave == clave) return n;
            return null;
        }

        /// <summary>Inserta una clave nueva. Devuelve false (sin modificar nada) si la clave ya existe.</summary>
        public bool Insertar(string clave, TValor valor)
        {
            if (clave == null) throw new ArgumentNullException("clave");
            if (BuscarNodo(clave) != null) return false;
            if ((double)(cantidad + 1) / cubetas.Length > FactorCargaMaximo) Redimensionar();

            int i = Hash(clave, cubetas.Length);
            var nuevo = new NodoHash<TValor> { Clave = clave, Valor = valor, Siguiente = cubetas[i] };
            cubetas[i] = nuevo;   // se inserta al inicio de la cadena: O(1)
            cantidad++;
            return true;
        }

        /// <summary>Inserta o reemplaza el valor de la clave.</summary>
        public void Establecer(string clave, TValor valor)
        {
            NodoHash<TValor> n = BuscarNodo(clave);
            if (n != null) n.Valor = valor;
            else Insertar(clave, valor);
        }

        public bool Buscar(string clave, out TValor valor)
        {
            NodoHash<TValor> n = clave == null ? null : BuscarNodo(clave);
            if (n == null) { valor = default(TValor); return false; }
            valor = n.Valor;
            return true;
        }

        /// <summary>Devuelve el valor o default(TValor) (null / 0) si no existe.</summary>
        public TValor Obtener(string clave)
        {
            NodoHash<TValor> n = clave == null ? null : BuscarNodo(clave);
            return n == null ? default(TValor) : n.Valor;
        }

        public bool Contiene(string clave)
        {
            return clave != null && BuscarNodo(clave) != null;
        }

        public bool Eliminar(string clave)
        {
            if (clave == null) return false;
            int i = Hash(clave, cubetas.Length);
            NodoHash<TValor> previo = null;
            NodoHash<TValor> actual = cubetas[i];
            while (actual != null)
            {
                if (actual.Clave == clave)
                {
                    if (previo == null) cubetas[i] = actual.Siguiente;
                    else previo.Siguiente = actual.Siguiente;
                    cantidad--;
                    return true;
                }
                previo = actual;
                actual = actual.Siguiente;
            }
            return false;
        }

        /// <summary>Rehashing: pasa a un tamaño primo mayor y reubica los nodos existentes.</summary>
        private void Redimensionar()
        {
            int nuevoTamano;
            if (indiceTamano + 1 < Tamanos.Length) { indiceTamano++; nuevoTamano = Tamanos[indiceTamano]; }
            else nuevoTamano = cubetas.Length * 2 + 1;

            var nuevas = new NodoHash<TValor>[nuevoTamano];
            for (int i = 0; i < cubetas.Length; i++)
            {
                NodoHash<TValor> n = cubetas[i];
                while (n != null)
                {
                    NodoHash<TValor> siguiente = n.Siguiente;
                    int j = Hash(n.Clave, nuevoTamano);
                    n.Siguiente = nuevas[j];
                    nuevas[j] = n;
                    n = siguiente;
                }
            }
            cubetas = nuevas;
        }

        public ListaEnlazada<TValor> ObtenerValores()
        {
            var lista = new ListaEnlazada<TValor>();
            for (int i = 0; i < cubetas.Length; i++)
                for (NodoHash<TValor> n = cubetas[i]; n != null; n = n.Siguiente)
                    lista.AgregarFinal(n.Valor);
            return lista;
        }

        public ListaEnlazada<string> ObtenerClaves()
        {
            var lista = new ListaEnlazada<string>();
            for (int i = 0; i < cubetas.Length; i++)
                for (NodoHash<TValor> n = cubetas[i]; n != null; n = n.Siguiente)
                    lista.AgregarFinal(n.Clave);
            return lista;
        }

        public int CubetasOcupadas
        {
            get
            {
                int c = 0;
                for (int i = 0; i < cubetas.Length; i++) if (cubetas[i] != null) c++;
                return c;
            }
        }

        public int CadenaMasLarga
        {
            get
            {
                int max = 0;
                for (int i = 0; i < cubetas.Length; i++)
                {
                    int largo = 0;
                    for (NodoHash<TValor> n = cubetas[i]; n != null; n = n.Siguiente) largo++;
                    if (largo > max) max = largo;
                }
                return max;
            }
        }

        /// <summary>Texto con el contenido de cada cubeta ocupada (útil para mostrar las colisiones).</summary>
        public string Imprimir()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tabla Hash: " + cantidad + " elementos, " + cubetas.Length + " cubetas, factor de carga "
                          + FactorCarga.ToString("0.00"));
            for (int i = 0; i < cubetas.Length; i++)
            {
                if (cubetas[i] == null) continue;
                sb.Append("[").Append(i.ToString("D4")).Append("] ");
                for (NodoHash<TValor> n = cubetas[i]; n != null; n = n.Siguiente)
                {
                    sb.Append(n.Clave);
                    if (n.Siguiente != null) sb.Append(" -> ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public void Limpiar()
        {
            indiceTamano = 0;
            cubetas = new NodoHash<TValor>[Tamanos[0]];
            cantidad = 0;
        }
    }
}
