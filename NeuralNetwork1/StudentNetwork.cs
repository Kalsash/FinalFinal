using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;
namespace NeuralNetwork1
{
    [ProtoContract]
    class Neuron
    {
        public static Func<double, double> activationFunction;
        public static Func<double, double> activationFunctionDerivative;
        [ProtoMember(1)]
        public int id;
        [ProtoMember(2)]
        public double Output;
        [ProtoMember(3)]
        public int layer;

        [ProtoMember(4)]
        public double error;

        // Веса связей от предыдущего слоя, где 0 элемент - bias, остальные - нейроны прошлого слоя в произвольном порядке (т.к. сеть полносвязная)
        [ProtoMember(5)]
        public double[] weightsToPrevLayer;

        public void setInput(double input)
        {
            if (layer == 0)
            {
                Output = input;
                return;
            }

            Output = activationFunction(input);
        }
        // public double Output
        // {
        //     get
        //     {
        //         if (layer == -1)
        //         {
        //             return 1;
        //         }
        //
        //         if (layer == 0)
        //         {
        //             return input;
        //         }
        //
        //         return activationFunction(input);
        //     }
        // }

        public Neuron(int id, int layer, int prevLayerCapacity, Random random)
        {
            this.id = id;
            this.layer = layer;
            this.error = 0;
            // Bias стабильно выдаёт 1
            if (layer == -1)
            {
                Output = 1;
            }


            // Веса с байасами инициализируем для всех слоёв, кроме входного и самого байаса
            if (layer < 1)
            {
                weightsToPrevLayer = null;
            }
            else
            {
                weightsToPrevLayer = new double[prevLayerCapacity + 1];
                for (int i = 0; i < weightsToPrevLayer.Length; i++)
                {
                    weightsToPrevLayer[i] = random.NextDouble() * 2 - 1;
                }
            }
        }

        public Neuron()
        {
        }
    }
    [ProtoContract]
    class Layer
    {
        [ProtoMember(1)]
        public Neuron[] neurons;

        public Neuron this[int index]
        {
            get => neurons[index];
            set => neurons[index] = value;
        }

        public Layer(int capacity)
        {
            neurons = new Neuron[capacity];
        }

        public Layer()
        {
        }

        public int Length => neurons.Length;

        public IEnumerable<Neuron> Select(Func<Neuron, Neuron> selector) => neurons.Select(selector);
        public IEnumerable<double> Select(Func<Neuron, double> selector) => neurons.Select(selector);
    }

    [ProtoContract]
    public class StudentNetwork:BaseNetwork
    {
        [ProtoMember(1)]
        private const double learningRate = 0.1;
        [ProtoMember(2)]
        private Neuron biasNeuron;
        [ProtoMember(3)]
        private List<Layer> layers;
        [ProtoMember(4)]
        public Func<double[], double[], double> lossFunction;
        [ProtoMember(5)]
        public Func<double, double, double> lossFunctionDerivative;

        public StudentNetwork()
        {
        }

        public static StudentNetwork readFromFile(string filename)
        {
            Neuron.activationFunction = s => 1.0 / (1.0 + Math.Exp(-s));
            Neuron.activationFunctionDerivative = s => s * (1 - s);
            StudentNetwork network;
            using (MemoryStream memStream = new MemoryStream())
            {
                var arrBytes = File.ReadAllBytes(filename);
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                network = Serializer.Deserialize<StudentNetwork>(memStream);
            }
            return network;
        }
        public StudentNetwork(int[] structure)
        {
            if (structure.Length < 3)
            {
                throw new ArgumentException("Сетка из 0 слоёв это круто, но не пойдёт");
            }

            lossFunction = (output, aim) =>
            {
                double res = 0;
                for (int i = 0; i < aim.Length; i++)
                {
                    res += Math.Pow(aim[i] - output[i], 2);
                }

                return res * 0.5; // / n to become MSE
            };

            lossFunctionDerivative = (output, aim) => aim - output;

            Neuron.activationFunction = s => 1.0 / (1.0 + Math.Exp(-s));
            Neuron.activationFunctionDerivative = s => s * (1 - s);

            Random random = new Random();

            biasNeuron = new Neuron(0, -1, -1, random);
            int id = 1;

            layers = new List<Layer>();

            for (int layer = 0; layer < structure.Length; layer++)
            {
                layers.Add(new Layer(structure[layer]));
                for (int i = 0; i < structure[layer]; i++)
                {
                    if (layer == 0)
                    {
                        layers[layer][i] = new Neuron(id, layer, -1, random);
                        continue;
                    }

                    layers[layer][i] = new Neuron(id, layer, structure[layer - 1], random);

                    id++;
                }
            }
        }


        public void forwardPropagation(double[] input)
        {
            if (input.Length != layers[0].Length)
            {
                throw new ArgumentException("Недопустимый входной массив");
            }

            // Копирование входных данных от сенсоров в выходы нейронов первого слоя:
            for (int i = 0; i < layers[0].Length; i++)
            {
                layers[0][i].setInput(input[i]);
            }
            //Для каждого слоя от второго до последнего
            for (int layer = 1; layer < layers.Count; layer++)
            {
                Parallel.For(0, layers[layer].Length, neuron =>
                {
                    // Считаем скалярное произведение между выходами нейронов 
                    // предыдущего слоя и их весами, взвешенными для текущего нейрона.
                    double scalar = 0;
                    // перебираем веса текущего нейрона для каждого нейрона предыдущего слоя.
                    for (int i = 0; i < layers[layer][neuron].weightsToPrevLayer.Length; i++)
                    {
                        // Обрабатываем bias
                        if (i == 0)
                        {
                            scalar += biasNeuron.Output * layers[layer][neuron].weightsToPrevLayer[0];
                            continue;
                        }

                        // вычисляется произведение взвешенного выхода нейрона
                        // предыдущего слоя и веса и добавляется к скаляру
                        scalar += layers[layer - 1][i - 1].Output * layers[layer][neuron].weightsToPrevLayer[i];
                    }

                    // скаляр становится входом для текущего нейрона
                    layers[layer][neuron].setInput(scalar);
                    //}
                });
            }
        }

        public void backwardPropagation(Sample sample)
        {
            var aim = sample.outputVector;
            // Для выходного слоя применяем производную лосс-функции
            Parallel.For(0, layers.Last().Length, i =>
            {
                layers.Last()[i].error = lossFunctionDerivative(layers.Last()[i].Output, aim[i]);
            });

            for (int layer = layers.Count - 1; layer >= 1; layer--)
            {
                for (int k = 0; k < layers[layer].Length; k++)
                {
                    var neuron = layers[layer][k];
                    // Применяем производную функции активации
                    neuron.error *= Neuron.activationFunctionDerivative(neuron.Output);

                    for (int i = 0; i < neuron.weightsToPrevLayer.Length; i++)
                    {
                        // Нельзя забывать про малыша bias!!!
                        if (i == 0)
                        {
                            biasNeuron.error += neuron.error * neuron.weightsToPrevLayer[0];
                            neuron.weightsToPrevLayer[0] += learningRate * neuron.error * biasNeuron.Output;
                            continue;
                        }

                        layers[layer - 1][i - 1].error += neuron.error * neuron.weightsToPrevLayer[i];
                        neuron.weightsToPrevLayer[i] += learningRate * neuron.error * layers[layer - 1][i - 1].Output;
                    }

                    // Мы прогнали ошибку дальше, откатываемся к изначальному виду
                    neuron.error = 0;
                }
            }
        }

        double TrainOnSample(Sample sample, double acceptableError)
        {
            double loss;
            forwardPropagation(sample.input);
            loss = lossFunction(layers.Last().Select(n => n.Output).ToArray(), sample.outputVector);
            backwardPropagation(sample);
            return loss;
        }
        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            int cnt = 0;
            while (true)
            {
                cnt++;
                //получение прогноза выходных данных
                forwardPropagation(sample.input);
                //Проверка условия остановки обучения
                if (lossFunction(layers.Last().Select(n => n.Output).ToArray(), sample.outputVector) <=
                    acceptableError || cnt > 50)
                {
                    return cnt;
                }
                Parallel.Invoke(() =>
                {
                    backwardPropagation(sample);
                });
            }
        }
        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError,
            bool parallel)
        {

            var start = DateTime.Now;// засекаем время
            int totalSamplesCount = epochsCount * samplesSet.Count; //общее количество обрабатываемых примеров
            int processedSamplesCount = 0;
            double sumError = 0; //суммарная ошибка
            double mean;//средняя ошибка
            // для каждой эпохи
            for (int epoch = 0; epoch < epochsCount; epoch++)
            {
                // для каждого сэмпла
                for (var index = 0; index < samplesSet.samples.Count; index++)
                {
                    var sample = samplesSet.samples[index];
                    sumError += TrainOnSample(sample, acceptableError);// добавляем новую ошибку обучения к суммарной ошибке
                    // Увеличение счетчика обработанных примеров
                    processedSamplesCount++;
                    // выводим информацию о процессе обучения
                    if (index % 100 == 0)
                    {
                        // Выводим среднюю ошибку для обработанного
                        OnTrainProgress(1.0 * processedSamplesCount / totalSamplesCount,
                            sumError / (epoch * samplesSet.Count + index + 1), DateTime.Now - start);
                    }
                }
                // Расчет средней ошибки для текущей эпохи
                mean = sumError / ((epoch + 1) * samplesSet.Count + 1);
                //Если средняя ошибка меньше или равна допустимой
                if (mean <= acceptableError)
                {
                    // выводим информацию о процессе обучения
                    OnTrainProgress(1.0, mean, DateTime.Now - start);
                    return mean;
                }
            }
            //средняя ошибка для всего обучающего набора данных
            mean = sumError / (epochsCount * samplesSet.Count + 1);
            // выводим информацию о процессе обучения
            OnTrainProgress(1.0, mean, DateTime.Now - start);
            //возвращается средняя ошибка 
            return sumError / (epochsCount * samplesSet.Count);
        }


        public override double[] Compute(double[] input)
        {
            if (input.Length != layers[0].Length)
            {
                throw new ArgumentException("У вас тут данных многовато...");
            }

            forwardPropagation(input);
            return layers.Last().Select(n => n.Output).ToArray();
        }

        public void save(string filename)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serializer.Serialize(ms, this);
                File.WriteAllBytes(filename, ms.ToArray());
            }
        }
    }

}