using System;
using System.Collections;
using System.Collections.Generic;

namespace TransporteUrbano.Estructuras
{
    /// <summary>
    /// [PERSONA A] Lista enlazada simple propia. Reemplaza a List&lt;T&gt; (prohibida por el enunciado).
    /// Implementa IEnumerable solo para poder usar foreach; la lógica es 100% propia.
    /// Complejidad: AgregarFinal/AgregarInicio O(1), indexador/Buscar/Eliminar O(n).
    /// </summary>
    public class ListaEnlazada<T> : IEnumerable<T>
    {
        private class Nodo
        {
            public T Dato;
            public Nodo Siguiente;
            public Nodo(T dato) { Dato = dato; }
        }

        private Nodo cabeza;
        private Nodo cola;
        private int cantidad;

        public int Cantidad { get { return cantidad; } }
        public bool EstaVacia { get { return cantidad == 0; } }

        public void AgregarFinal(T dato)
        {
            var nuevo = new Nodo(dato);
            if (cola == null) { cabeza = nuevo; cola = nuevo; }
            else { cola.Siguiente = nuevo; cola = nuevo; }
            cantidad++;
        }

        public void AgregarInicio(T dato)
        {
            var nuevo = new Nodo(dato);
            nuevo.Siguiente = cabeza;
            cabeza = nuevo;
            if (cola == null) cola = nuevo;
            cantidad++;
        }

        public T this[int indice]
        {
            get
            {
                if (indice < 0 || indice >= cantidad) throw new IndexOutOfRangeException();
                Nodo actual = cabeza;
                for (int i = 0; i < indice; i++) actual = actual.Siguiente;
                return actual.Dato;
            }
        }

        /// <summary>Elimina el primer elemento que cumpla la condición. Devuelve true si eliminó algo.</summary>
        public bool Eliminar(Func<T, bool> condicion)
        {
            Nodo previo = null;
            Nodo actual = cabeza;
            while (actual != null)
            {
                if (condicion(actual.Dato))
                {
                    if (previo == null) cabeza = actual.Siguiente;
                    else previo.Siguiente = actual.Siguiente;
                    if (actual == cola) cola = previo;
                    cantidad--;
                    return true;
                }
                previo = actual;
                actual = actual.Siguiente;
            }
            return false;
        }

        public T Buscar(Func<T, bool> condicion)
        {
            for (Nodo n = cabeza; n != null; n = n.Siguiente)
                if (condicion(n.Dato)) return n.Dato;
            return default(T);
        }

        public bool Existe(Func<T, bool> condicion)
        {
            for (Nodo n = cabeza; n != null; n = n.Siguiente)
                if (condicion(n.Dato)) return true;
            return false;
        }

        public void Limpiar()
        {
            cabeza = null;
            cola = null;
            cantidad = 0;
        }

        public T[] ToArray()
        {
            var arreglo = new T[cantidad];
            int i = 0;
            for (Nodo n = cabeza; n != null; n = n.Siguiente) arreglo[i++] = n.Dato;
            return arreglo;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (Nodo n = cabeza; n != null; n = n.Siguiente) yield return n.Dato;
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
