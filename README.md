# Sistema de Transporte Urbano y Red Vial Nacional
**Curso:** Estructura de Datos II  
**Catedrático:** Ing. Manuel Gustavo Rojas  
**Lenguaje:** C# (.NET 8.0 / Windows Forms)  
**Modalidad:** Proyecto de Laboratorio No. 2 (Grupal)

---

## 1. Descripción del Proyecto y Escenario

El sistema modela una red de transporte interurbana y nacional (enfocada en Quetzaltenango y conexiones estratégicas de Guatemala). Permite la gestión de paradas (estaciones), el registro de rutas viales ponderadas por tiempo y costo, la consulta en tiempo real mediante autocompletado, la verificación inmediata de conectividad entre sectores aislados y el cálculo de trayectorias óptimas.

La arquitectura desacopla la capa de interfaz gráfica (`Windows Forms` / GDI+) de la lógica algorítmica mediante un contrato de interfaz (`IGrafoTransporte`), garantizando modularidad, escalabilidad e interoperabilidad.

---

## 2. Estructuras de Datos Utilizadas y Justificación

El núcleo del sistema integra cuatro estructuras de datos personalizadas, construidas desde cero sin utilizar librerías de colecciones avanzadas de .NET (como `SortedDictionary` o `PriorityQueue`):

| Estructura | Módulo / Aplicación en el Sistema | Complejidad Temporal | Complejidad Espacial | Justificación Técnica |
| :--- | :--- | :---: | :---: | :--- |
| **Tabla Hash** | Indexación y búsqueda de registros de `Parada` por código único (ID). | $O(1)$ promedio | $O(V)$ | Acceso inmediato a la información completa de cualquier nodo sin escaneo secuencial. Factor de carga de 0.75 y resolución de colisiones mediante encadenamiento. |
| **Árbol Trie** | Motor de autocompletado en el buscador interactivo de paradas por nombre. | $O(L)$ ($L$ = longitud del texto) | $O(\Sigma \cdot N \cdot L)$ | Permite sugerir estaciones en tiempo real a medida que el usuario teclea, sin verse afectado por la cantidad total de registros en la red. |
| **Union-Find (Disjoint Set)** | Verificación de conectividad y detección de componentes conexas (Zonas). | $O(\alpha(V)) \approx O(1)$ | $O(V)$ | Barrera lógica de validación previa: impide que el algoritmo de Dijkstra se ejecute en grafos disconexos, ahorrando ciclos de CPU. Implementa compresión de caminos. |
| **Grafo (Listas de Adyacencia)** | Modelado de la red vial, recorridos y cálculo de caminos mínimos. | $O(V + E)$ | $O(V + E)$ | Optimización estricta de memoria para grafos dispersos (*sparse graphs*), evitando el desperdicio espacial $O(V^2)$ propio de matrices de adyacencia. |
| **MinHeap (Montículo Binario)** | Cola de prioridad propia integrada al algoritmo de Dijkstra. | $O(\log V)$ por extracción/inserción | $O(V)$ | Reemplaza la búsqueda lineal del vértice mínimo ($O(V)$), optimizando el tiempo total de Dijkstra a $O(E \log V)$. Implementa operaciones `Flotar` y `Hundir`. |

---

## 3. Análisis de Trade-Offs (Compromisos de Diseño)

En el diseño e integración del sistema se asumieron las siguientes decisiones de ingeniería:

1. **Trie vs. Búsqueda Secuencial (`Contains` en Lista):**
   * *Ganancia:* Búsqueda de prefijos ultraveloz en tiempo $O(L)$, permitiendo autocompletado fluido en la interfaz.
   * *Sacrificio:* Consumo de memoria RAM significativamente mayor, debido a la creación de múltiples nodos y punteros por cada carácter del alfabeto.
2. **Union-Find como Filtro previo a Dijkstra:**
   * *Ganancia:* Tiempo de respuesta casi instantáneo ($O(\alpha(V))$) para rechazar rutas inviables entre zonas aisladas.
   * *Sacrificio:* Memoria auxiliar para mantener arreglos de representantes/padres y sobrecosto de actualización en cada inserción de aristas.
3. **Listas de Adyacencia con Tabla Hash vs. Matriz de Adyacencia:**
   * *Ganancia:* Eficiencia espacial $O(V + E)$. En una red de 30 paradas, una matriz reserva 900 celdas (la mayoría ceros); la lista solo almacena las conexiones reales.
   * *Sacrificio:* Mayor complejidad en la gestión de punteros y costo ligeramente superior para verificar la existencia de una arista específica ($O(\text{grado}(u))$ vs $O(1)$ directo).
4. **MinHeap Propio vs. Búsqueda Lineal:**
   * *Ganancia:* Reducción de complejidad temporal de $O(V^2)$ a $O(E \log V)$.
   * *Sacrificio:* Complejidad de codificación y mantenimiento de las propiedades de árbol balanceado en memoria mediante arreglos dinámicos.

---

## 4. Instrucciones de Compilación y Ejecución

### Prerrequisitos
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o superior.
* Sistema Operativo Windows (requerido para el renderizado de `Windows Forms`).
* Visual Studio 2022 o Visual Studio Code con extensión de C#.

### Pasos de Instalación y Despliegue
1. Clonar el repositorio público:
   ```bash
   git clone [https://github.com/DanSoberanis/Proyecto-Final-EDDII](https://github.com/DanSoberanis/Proyecto-Final-EDDII)
   cd TransporteUrbano
