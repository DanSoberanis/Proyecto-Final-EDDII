using System;
using System.Collections.Generic;
using TransporteUrbano.Models;

namespace TransporteUrbano.Estructuras
{
    // Estructura interna para almacenar las conexiones y pesos
    public class AristaGrafo
    {
        public string Destino { get; set; }
        public double Tiempo { get; set; }
        public double Costo { get; set; }
    }

    // El Montículo Binario adaptado para operar con identificadores tipo string
    public class NodoHeapStr
    {
        public string Vertice { get; set; }
        public double Distancia { get; set; }
        public string VerticePrevio { get; set; } 
    }

    public class MinHeapStr
    {
        private NodoHeapStr[] monticulo;
        private int cantidad;

        public MinHeapStr(int capacidad = 128)
        {
            monticulo = new NodoHeapStr[capacidad];
            cantidad = 0;
        }

        public bool EstaVacio => cantidad == 0;

        public void Insertar(string vertice, double distancia, string previo)
        {
            if (cantidad == monticulo.Length) Redimensionar();
            monticulo[cantidad] = new NodoHeapStr { Vertice = vertice, Distancia = distancia, VerticePrevio = previo };
            Flotar(cantidad);
            cantidad++;
        }

        public NodoHeapStr ExtraerMinimo()
        {
            if (EstaVacio) return null;
            NodoHeapStr minimo = monticulo[0];
            cantidad--;
            monticulo[0] = monticulo[cantidad];
            monticulo[cantidad] = null; 
            if (cantidad > 0) Hundir(0);
            return minimo;
        }

        private void Flotar(int indice)
        {
            while (indice > 0)
            {
                int padre = (indice - 1) / 2;
                if (monticulo[indice].Distancia >= monticulo[padre].Distancia) break;
                Intercambiar(indice, padre);
                indice = padre;
            }
        }

        private void Hundir(int indice)
        {
            while (indice < cantidad)
            {
                int hijoIzq = 2 * indice + 1;
                int hijoDer = 2 * indice + 2;
                int menor = indice;

                if (hijoIzq < cantidad && monticulo[hijoIzq].Distancia < monticulo[menor].Distancia) menor = hijoIzq;
                if (hijoDer < cantidad && monticulo[hijoDer].Distancia < monticulo[menor].Distancia) menor = hijoDer;

                if (menor == indice) break;
                Intercambiar(indice, menor);
                indice = menor;
            }
        }

        private void Intercambiar(int i, int j)
        {
            NodoHeapStr temp = monticulo[i];
            monticulo[i] = monticulo[j];
            monticulo[j] = temp;
        }

        private void Redimensionar()
        {
            NodoHeapStr[] nuevo = new NodoHeapStr[monticulo.Length * 2];
            for (int i = 0; i < cantidad; i++) nuevo[i] = monticulo[i];
            monticulo = nuevo;
        }
    }

    // Clase principal que firma el contrato con la interfaz gráfica de Persona A
    public class GrafoTransporte : IGrafoTransporte
    {
        // Se utiliza la TablaHash de la Persona A para manejar las listas de adyacencia de forma dinámica
        private TablaHash<ListaEnlazada<AristaGrafo>> adyacencia;

        public GrafoTransporte()
        {
            adyacencia = new TablaHash<ListaEnlazada<AristaGrafo>>();
        }

        public bool Disponible => true;
        public string Nombre => "Grafo con Lista de Adyacencia y Dijkstra optimizado (Persona B)";

        public void AgregarVertice(string codigo)
        {
            if (!adyacencia.Contiene(codigo))
            {
                adyacencia.Insertar(codigo, new ListaEnlazada<AristaGrafo>());
            }
        }

        public void EliminarVertice(string codigo)
        {
            adyacencia.Eliminar(codigo);
            ListaEnlazada<string> claves = adyacencia.ObtenerClaves();
            
            // Eliminar todas las aristas incidentes hacia este vértice
            foreach (string clave in claves)
            {
                ListaEnlazada<AristaGrafo> vecinos = adyacencia.Obtener(clave);
                vecinos?.Eliminar(a => a.Destino == codigo);
            }
        }

        public void AgregarArista(Ruta ruta)
        {
            ListaEnlazada<AristaGrafo> origen = adyacencia.Obtener(ruta.Origen);
            origen?.AgregarFinal(new AristaGrafo { Destino = ruta.Destino, Tiempo = ruta.Tiempo, Costo = ruta.Costo });

            if (!ruta.Unidireccional)
            {
                ListaEnlazada<AristaGrafo> destino = adyacencia.Obtener(ruta.Destino);
                destino?.AgregarFinal(new AristaGrafo { Destino = ruta.Origen, Tiempo = ruta.Tiempo, Costo = ruta.Costo });
            }
        }

        public void EliminarArista(Ruta ruta)
        {
            ListaEnlazada<AristaGrafo> origen = adyacencia.Obtener(ruta.Origen);
            origen?.Eliminar(a => a.Destino == ruta.Destino);

            if (!ruta.Unidireccional)
            {
                ListaEnlazada<AristaGrafo> destino = adyacencia.Obtener(ruta.Destino);
                destino?.Eliminar(a => a.Destino == ruta.Origen);
            }
        }

        public ResultadoRuta Dijkstra(string origen, string destino, CriterioPeso criterio)
        {
            TablaHash<double> distancias = new TablaHash<double>();
            TablaHash<string> previos = new TablaHash<string>();
            TablaHash<bool> procesados = new TablaHash<bool>();

            ListaEnlazada<string> vertices = adyacencia.ObtenerClaves();
            foreach (string v in vertices)
            {
                distancias.Insertar(v, double.MaxValue);
                procesados.Insertar(v, false);
            }
            distancias.Establecer(origen, 0);

            MinHeapStr heap = new MinHeapStr();
            heap.Insertar(origen, 0, null);

            while (!heap.EstaVacio)
            {
                NodoHeapStr actual = heap.ExtraerMinimo();
                string u = actual.Vertice;

                if (procesados.Obtener(u)) continue;
                procesados.Establecer(u, true);

                if (u == destino) break;

                ListaEnlazada<AristaGrafo> vecinos = adyacencia.Obtener(u);
                if (vecinos != null)
                {
                    foreach (AristaGrafo arista in vecinos)
                    {
                        string v = arista.Destino;
                        if (procesados.Obtener(v)) continue;

                        double peso = (criterio == CriterioPeso.Tiempo) ? arista.Tiempo : arista.Costo;
                        double nuevaDistancia = distancias.Obtener(u) + peso;

                        if (nuevaDistancia < distancias.Obtener(v))
                        {
                            distancias.Establecer(v, nuevaDistancia);
                            previos.Establecer(v, u);
                            heap.Insertar(v, nuevaDistancia, u);
                        }
                    }
                }
            }

            ResultadoRuta res = new ResultadoRuta();
            if (distancias.Obtener(destino) == double.MaxValue)
            {
                res.Exito = false;
                res.Mensaje = "Dijkstra: No se encontró un camino viable hacia el destino.";
                return res;
            }

            res.Exito = true;
            res.PesoTotal = distancias.Obtener(destino);
            res.Camino = new ListaEnlazada<string>();

            // Reconstruir la ruta inversa
            string nodoActual = destino;
            while (nodoActual != null)
            {
                res.Camino.AgregarInicio(nodoActual);
                nodoActual = previos.Obtener(nodoActual);
            }

            return res;
        }

        public ListaEnlazada<string> BFS(string inicio)
        {
            ListaEnlazada<string> resultado = new ListaEnlazada<string>();
            TablaHash<bool> visitados = new TablaHash<bool>();
            Queue<string> cola = new Queue<string>();

            cola.Enqueue(inicio);
            visitados.Insertar(inicio, true);

            while (cola.Count > 0)
            {
                string actual = cola.Dequeue();
                resultado.AgregarFinal(actual);

                ListaEnlazada<AristaGrafo> vecinos = adyacencia.Obtener(actual);
                if (vecinos != null)
                {
                    foreach (AristaGrafo arista in vecinos)
                    {
                        if (!visitados.Contiene(arista.Destino))
                        {
                            visitados.Insertar(arista.Destino, true);
                            cola.Enqueue(arista.Destino);
                        }
                    }
                }
            }
            return resultado;
        }

        public ListaEnlazada<string> DFS(string inicio)
        {
            ListaEnlazada<string> resultado = new ListaEnlazada<string>();
            TablaHash<bool> visitados = new TablaHash<bool>();
            DFSUtil(inicio, visitados, resultado);
            return resultado;
        }

        private void DFSUtil(string actual, TablaHash<bool> visitados, ListaEnlazada<string> resultado)
        {
            visitados.Insertar(actual, true);
            resultado.AgregarFinal(actual);

            ListaEnlazada<AristaGrafo> vecinos = adyacencia.Obtener(actual);
            if (vecinos != null)
            {
                foreach (AristaGrafo arista in vecinos)
                {
                    if (!visitados.Contiene(arista.Destino))
                    {
                        DFSUtil(arista.Destino, visitados, resultado);
                    }
                }
            }
        }

        public string Imprimir()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Lista de Adyacencia del Grafo (Persona B):");
            
            ListaEnlazada<string> claves = adyacencia.ObtenerClaves();
            foreach (string clave in claves)
            {
                sb.Append(clave).Append(" -> ");
                ListaEnlazada<AristaGrafo> vecinos = adyacencia.Obtener(clave);
                
                bool primero = true;
                if (vecinos != null)
                {
                    foreach (AristaGrafo a in vecinos)
                    {
                        if (!primero) sb.Append(", ");
                        sb.Append(a.Destino).Append($"({a.Tiempo}m, Q{a.Costo})");
                        primero = false;
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}