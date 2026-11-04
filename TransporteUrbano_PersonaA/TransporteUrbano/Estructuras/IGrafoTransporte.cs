using TransporteUrbano.Models;

namespace TransporteUrbano.Estructuras
{
    public interface IGrafoTransporte
    {
        bool Disponible { get; }
        string Nombre { get; }

        void AgregarVertice(string codigo);
        void EliminarVertice(string codigo);
        void AgregarArista(Ruta ruta);
        void EliminarArista(Ruta ruta);

        ResultadoRuta Dijkstra(string origen, string destino, CriterioPeso criterio);
        
        ListaEnlazada<string> BFS(string inicio);
        ListaEnlazada<string> DFS(string inicio);
        
        string Imprimir();
    }
}