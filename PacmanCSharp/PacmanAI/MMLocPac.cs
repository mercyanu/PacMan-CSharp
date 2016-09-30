﻿using Accord.Neuro;
using Accord.Neuro.ActivationFunctions;
using Accord.Neuro.Learning;
using Accord.Neuro.Networks;
using Pacman.GameLogic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MMPac
{
    public class MMLocPac : BasePacman
    {
        public DeepBeliefNetwork Network;

        static int InputCount = 12;
        static int OutputCount = 1;

        EvolutionWeights EvoWeights;


        List<Node> PathToTake = new List<Node>();
        Node bestNode = null;

        //List<double> PreviousOutput = new List<double>();

        public MMLocPac(List<double> NNWeights)
            : base("MMLocPac")
        {
            Network = new DeepBeliefNetwork(new BernoulliFunction(), InputCount, 10, OutputCount);

            EvoWeights = new EvolutionWeights(Network);
            EvoWeights.SetWeights(NNWeights);
            Network.UpdateVisibleWeights();

            //for (int i = 0; i < OutputCount; i++) PreviousOutput.Add(0);
        }

        public MMLocPac(string LoadFromFile = "")
            : base("MMLocPac")
        {
            if (LoadFromFile.Length > 0)
            {
                EvoWeights = new EvolutionWeights(null);
                Network = EvoWeights.LoadWeightsFromFile(LoadFromFile);
            }
            else
            {
                Network = new DeepBeliefNetwork(new BernoulliFunction(), InputCount, 10, OutputCount);
            }

            //for (int i = 0; i < OutputCount; i++) PreviousOutput.Add(0);
        }

        public void SaveWeights(string filename)
        {
            EvoWeights.SaveWeights(filename);
        }

        public List<double> GenerateInput(GameState gs, Node P)
        {
            List<double> input = new List<double>();
            //int PacX = gs.Pacman.Node.X;
            //int PacY = gs.Pacman.Node.Y;
            //Node P = gs.Pacman.Node;

            var PathtoPac = P.ShortestPath[gs.Pacman.Node.X, gs.Pacman.Node.Y];
            input.Add((PathtoPac != null ? PathtoPac.Distance : 100));

            //IsJunction
            input.Add(P.PossibleDirections.Count > 2 ? 1 : -1);

            foreach (var G in gs.Ghosts)
            {
                var Path = P.ShortestPath[G.Node.X, G.Node.Y];
                input.Add((Path != null ? Path.Distance : 100));
                //input.Add(Path != null ? ((double)Path.Direction) : 1);
                input.Add((!G.Chasing && G.Entered) ? 1 : -1);

                //input.Add(Normalize(PacX - G.Node.X, 32)); input.Add(Normalize(PacY - G.Node.Y, 32));
                
                //Distance to ghost
                //input.Add(Normalize(Math.Abs(PacX - G.Node.X) + Math.Abs(PacY - G.Node.Y), 64));
            }
            
            var NearestPowerPill = StateInfo.NearestPowerPill(P, gs);
            if (NearestPowerPill.Target != null)
            {
                var Path = P.ShortestPath[NearestPowerPill.Target.X, NearestPowerPill.Target.Y];
                input.Add((Path != null ? Path.Distance : 100));
                //input.Add(Path != null ? ((double)Path.Direction) : 1);
                //input.Add(Normalize(PacX - NearestPowerPill.Target.X, 32));
                //input.Add(Normalize(PacY - NearestPowerPill.Target.Y, 32));
            }
            else
            {
                input.Add(-100);
                //input.Add(1);
            }

            if(P.Type == Node.NodeType.Pill)
            {
                input.Add(0);
            } else
            {
                NearestPowerPill = StateInfo.NearestPill(P, gs);
                if (NearestPowerPill.Target != null)
                {
                    var Path = P.ShortestPath[NearestPowerPill.Target.X, NearestPowerPill.Target.Y];
                    input.Add((Path != null ? Path.Distance : 2000));
                    //input.Add(Path != null ? ((double)Path.Direction) : 1);
                    //input.Add(Normalize(PacX - NearestPowerPill.Target.X, 32));
                    //input.Add(Normalize(PacY - NearestPowerPill.Target.Y, 32));
                }
                else
                {
                    input.Add(-100);
                    //input.Add(0);
                }
            }
            return input;
        }

        public override Direction Think(GameState gs)
        {
            double bestScore = -10000;
            //double[][] Scores = new double[gs.Map.Nodes.GetLength(0)][];

            //List<Direction> possible = gs.Pacman.PossibleDirections();

            foreach (var Node in gs.Map.Nodes)
            //foreach(var Dir in possible)
            {
                if (Node != gs.Pacman.Node && Node.ShortestPath[gs.Pacman.Node.X, gs.Pacman.Node.Y] != null)
                {
                    //Node Node = gs.Pacman.Node.GetNeighbour(Dir);
                    List<double> input = GenerateInput(gs, Node);
                    double output = Network.Compute(input.ToArray())[0];
                    //Scores[i][y] = output;
                    if (output > bestScore)
                    {
                        bestScore = output;
                        bestNode = Node;
                    }
                }
            }

            //List<double> input = GenerateInput(gs);
            
            //double[] output = Network.Compute(input.ToArray());

            //PreviousOutput.Clear();
            //PreviousOutput.AddRange(output);

            Direction Res = Direction.None;

            /*List<Direction> possible = gs.Pacman.PossibleDirections();
            possible.Add(Direction.Stall);

            for(int i=0;i< OutputCount; i++)
            {
                if(!possible.Contains((Direction)i))
                {
                    output[i] = 0;
                }
            }

            if (possible.Count > 0)
            {
                var indexAtMax = output.ToList().IndexOf(output.Max());
                Res = (Direction)(indexAtMax);
            }*/

            //comment this out to remove A* search
            bestNode = AStarGetBestRoute(gs, gs.Pacman.Node, bestNode);

            var Path = gs.Pacman.Node.ShortestPath[bestNode.X, bestNode.Y];
            if(Path != null)
            {
                Res = Path.Direction;
            }

            return Res;
        }

        public static float Normalize(float Input, float Max)
        {
            return Input / Max;
        }

        public Node AStarGetBestRoute(GameState gs, Node startingPoint, Node to)
        {

            Dictionary<Node,float> priorityQueue = new Dictionary<Node, float>();

            priorityQueue.Add(startingPoint,0);

            Dictionary<Node, Node> came_from = new Dictionary<Node, Node>();
            Dictionary<Node, float> cost_so_far = new Dictionary<Node, float>();
            came_from[startingPoint] = null;
            cost_so_far[startingPoint] = 0;


            Node current = null;

            while(!(priorityQueue.Count == 0))
            {
                var min = priorityQueue.Min(p => p.Value);
                current = priorityQueue.Where(p => p.Value == min).First().Key;
                priorityQueue.Remove(current);

                if (current == to)
                {
                    break;
                }

                foreach(var neighbour in current.PossibleDirections)
                {
                    float pass_cost = 10;
                    if(neighbour.Type == Node.NodeType.Pill)
                    {
                        pass_cost = 5;
                    }

                    foreach(var g in gs.Ghosts)
                    {
                        var Path = g.Node.ShortestPath[neighbour.X, neighbour.Y];
                        if (Path != null && Path.Distance < 2)
                        {
                            pass_cost = (g.Fleeing ? 1 : 2000);
                        }
                    }
                    float new_cost = cost_so_far[current] + pass_cost;

                    if((!cost_so_far.ContainsKey(neighbour)) || (new_cost < cost_so_far[neighbour]))
                    {
                        cost_so_far[neighbour] = new_cost;

                        var Path = startingPoint.ShortestPath[neighbour.X, neighbour.Y];

                        if (priorityQueue.ContainsKey(neighbour))
                        {
                            priorityQueue[neighbour] = new_cost + Path.Distance;
                        }
                        else
                        {
                            priorityQueue.Add(neighbour, new_cost + Path.Distance);
                        }

                        came_from[neighbour] = current;
                    }
                }
            }

            PathToTake.Clear();

            if (cost_so_far.ContainsKey(to))
            {
                var path = to;
                do
                {
                    PathToTake.Add(path);

                    if(came_from[path] == startingPoint)
                    {
                        return path;
                    }

                    path = came_from[path];

                } while (path != startingPoint);
            }

            return null;
        }

        public override void Draw(Graphics g)
        {
            Node start = PathToTake.First();
            Node end = PathToTake.Last();

            g.DrawLine(new Pen(Color.Red, 5f),
                               new Point(start.CenterX, start.CenterY),
                               new Point(end.CenterX, end.CenterY));

            if(bestNode != null)
            {
                g.DrawLine(new Pen(Color.Green, 5f),
                               new Point(start.CenterX, start.CenterY),
                               new Point(bestNode.CenterX, bestNode.CenterY));
            }

            // Make sure that the parent is not null before we attempt this
            /*if (m_Parent != null)
            {
                if (m_Parent.PathNode != null)
                {
                    // Depending on whether or not the path is a bad one, choose the appropriate colour to use.
                    Brush _drawbrush = AverageScore < 0 ? Brushes.Red : Brushes.Green;

                    // Draw a line from this point to another.
                    g.DrawLine(new Pen(_drawbrush, 5f),
                               new Point(this.m_CurrentPosition.CenterX, this.m_CurrentPosition.CenterY),
                               new Point(m_Parent.PathNode.CenterX, m_Parent.PathNode.CenterY));

                    g.DrawImage(LucPac.m_GreenBlock, new Point(this.m_CurrentPosition.CenterX - 2, this.m_CurrentPosition.CenterY - 2));
                    g.DrawString(AverageScore.ToString(), new Font(FontFamily.GenericSansSerif, 10f), Brushes.White, m_CurrentPosition.CenterX, m_CurrentPosition.CenterY);

                    // Output the last direction that was taken to get to the node that we are after.
                    //g.DrawString(m_Directions[m_Directions.Length - 1].ToString(), new Font(FontFamily.GenericSansSerif, 10f), Brushes.White, m_CurrentPosition.CenterX, m_CurrentPosition.CenterY);
                }
            }*/
        }
    }
}