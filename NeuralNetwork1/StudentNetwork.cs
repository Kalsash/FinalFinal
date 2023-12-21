using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NeuralNetwork1
{
    class Neuron
    {
        public static Func<double, double> activationFunction; // активационная функция
        public static Func<double, double> activationFunctionDerivative; // ее производная

        public int id;
        public double Output;
        public int layer; // номер слоя

        public double error;

        // Веса связей от предыдущего слоя, где 0 элемент - bias, остальные - нейроны прошлого слоя в произвольном порядке (т.к. сеть полносвязная)
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

        public Neuron(int id, int layer, int prevLayerCapacity, Random random)
        {
            this.id = id;
            this.layer = layer;
            this.error = 0;
            // Bias стабильно выдаёт 1 
            if (layer == -1)//нейрон находится на байас слое
            {
                Output = 1;
            }

            // Веса с байасами инициализируем для всех слоёв, кроме входного и самого байаса
            if (layer < 1)// нейрон находится на входном или байес слое
            {
                weightsToPrevLayer = null;
            }
            else // нейрон находится на скрытом или выходном слое
            {
                weightsToPrevLayer = new double[prevLayerCapacity + 1];
                for (int i = 0; i < weightsToPrevLayer.Length; i++)
                {
                    //веса связей с предыдущим слоем
                    weightsToPrevLayer[i] = random.NextDouble() * 2 - 1;
                }
            }
        }
    }

    public class StudentNetwork : BaseNetwork
    {
        private const int hiddenLayersCount = 2;// количество скрытых слоев
        private const double learningRate = 0.1; //скорость обучения

        private Neuron biasNeuron;
        private List<Neuron[]> layers;
        //Функция потерь измеряет разницу между прогнозируемыми и
        //целевыми значениями и используется в процессе обучения для настройки весов нейронов.
        private Func<double[], double[], double> lossFunction;
        private Func<double, double, double> lossFunctionDerivative;

        public StudentNetwork(int[] structure)
        {
            if (structure.Length < 3)
            {
                throw new ArgumentException("Слишком мало слоев");
            }

            lossFunction = (output, aim) =>
            {
                double res = 0;
                for (int i = 0; i < aim.Length; i++)
                {
                    res += Math.Pow(aim[i] - output[i], 2);
                }

                return res * 0.5; // / для получения среднеквадратичной ошибки MSE
            };
            // производная            
            lossFunctionDerivative = (output, aim) => aim - output;

            Neuron.activationFunction = s => 1.0 / (1.0 + Math.Exp(-s));
            Neuron.activationFunctionDerivative = s => s * (1 - s);

            Random random = new Random();

            biasNeuron = new Neuron(0, -1, -1, random);
            int id = 1;

            layers = new List<Neuron[]>();

            for (int layer = 0; layer < structure.Length; layer++)
            {
                //массив нейронов для текущего слоя 
                layers.Add(new Neuron[structure[layer]]);
                //Для каждого нейрона в текущем слое
                for (int i = 0; i < structure[layer]; i++)
                {
                    //слой входных нейронов
                    if (layer == 0)
                    {
                        layers[layer][i] = new Neuron(id, layer, -1, random);
                        continue;
                    }
                    //количеством входов равно количеству нейронов в предыдущем слое structure
                    layers[layer][i] = new Neuron(id, layer, structure[layer - 1], random);

                    id++;
                }
            }
        }

        // выполняет прямое распространение сигнала по сети. Принимает входные данные input и
        // устанавливает значения активаций нейронов входного слоя. Затем происходит вычисление
        // активаций нейронов остальных слоев.
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

        //выполняет обратное распространение ошибки через сеть. Принимает образец sample с целевым
        //выходом и вычисляет ошибку для каждого нейрона в каждом слое. Затем происходит корректировка
        //весов нейронов с использованием градиентного спуска.
        public void backwardPropagation(Sample sample)
        {
            // Получение целевого вектора ожидаемых выходных данных для обучающего примера sample
            var aim = sample.outputVector;

            // Для выходного слоя применяем производную лосс-функции
            //для каждого нейрона в последнем слое
            Parallel.For(0, layers.Last().Length, i =>
            {
                layers.Last()[i].error = lossFunctionDerivative(layers.Last()[i].Output, aim[i]);
            });
            // для каждого нейрона остальных слоев
            for (int layer = layers.Count - 1; layer >= 1; layer--)
            {
                Parallel.ForEach(layers[layer], neuron =>
                {
                    // Применяем производную функции активации
                    neuron.error *= Neuron.activationFunctionDerivative(neuron.Output);

                    for (int i = 0; i < neuron.weightsToPrevLayer.Length; i++)
                    {
                        // bias
                        if (i == 0)
                        {
                            biasNeuron.error += neuron.error * neuron.weightsToPrevLayer[0];
                            // обновляем вес, добавляя шаг обучения
                            neuron.weightsToPrevLayer[0] += learningRate * neuron.error * biasNeuron.Output;
                            continue;
                        }

                        layers[layer - 1][i - 1].error += neuron.error * neuron.weightsToPrevLayer[i];
                        neuron.weightsToPrevLayer[i] += learningRate * neuron.error * layers[layer - 1][i - 1].Output;
                    }

                    // Сброс ошибки нейронов
                    neuron.error = 0;
                });
            }
        }
        //выполняет обучение сети на одном образце sample до достижения заданной допустимой ошибки acceptableError. 
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
                if (parallel)
                {
                    Parallel.Invoke(() =>
                    {
                        backwardPropagation(sample);
                    });
                }
                else
                {
                    backwardPropagation(sample);
                }
            }
        }

        //выполняет одну итерацию обучения нейронной сети на одном образце sample, вычисляет и возвращает ошибку
        double TrainOnSample(Sample sample, double acceptableError)
        {

            double loss;
            //получение прогноза выходных данных
            forwardPropagation(sample.input);
            // Вычисление значения ошибки
            loss = lossFunction(layers.Last().Select(n => n.Output).ToArray(), sample.outputVector);
            //Обратное распространение ошибки для обновления весов и смещений в нейронной сети на основе значения ошибки
            backwardPropagation(sample);
            //возвращение значения ошибки
            return loss;
        }

        //выполняет обучение на наборе образцов samplesSet в течение заданного количества эпох epochsCount
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

        protected override double[] Compute(double[] input)
        {
            if (input.Length != layers[0].Length)
            {
                throw new ArgumentException("Недопустимый входной массив");
            }
            // прогноз выходных данных
            forwardPropagation(input);
            // Возвращение выходного вектора последнего слоя нейронной сети
            return layers.Last().Select(n => n.Output).ToArray();
        }
    }
}