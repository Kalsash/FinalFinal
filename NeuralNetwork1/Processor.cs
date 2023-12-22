using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Accord.Math;

namespace NeuralNetwork1
{
    internal class Settings
    {
        private int _border = 20;
        public int border
        {
            get
            {
                return _border;
            }
            set
            {
                if ((value > 0) && (value < height / 3))
                {
                    _border = value;
                    if (top > 2 * _border) top = 2 * _border;
                    if (left > 2 * _border) left = 2 * _border;
                }
            }
        }

        public int width = 640;
        public int height = 640;

        /// <summary>
        /// Размер сетки для сенсоров по горизонтали
        /// </summary>
        public int blocksCount = 10;

        /// <summary>
        /// Желаемый размер изображения до обработки
        /// </summary>
        public Size orignalDesiredSize = new Size(500, 500);
        /// <summary>
        /// Желаемый размер изображения после обработки
        /// </summary>
        public Size processedDesiredSize = new Size(300, 300);

        public int margin = 10;
        public int top = 40;
        public int left = 40;

        /// <summary>
        /// Второй этап обработки
        /// </summary>
        public bool processImg = false;

        /// <summary>
        /// Порог при отсечении по цвету 
        /// </summary>
        public byte threshold = 120;
        public float differenceLim = 0.15f;

        public void incTop() { if (top < 2 * _border) ++top; }
        public void decTop() { if (top > 0) --top; }
        public void incLeft() { if (left < 2 * _border) ++left; }
        public void decLeft() { if (left > 0) --left; }
    }

    internal class MagicEye
    {
        /// <summary>
        /// Обработанное изображение
        /// </summary>
        public Bitmap processed;
        /// <summary>
        /// Оригинальное изображение после обработки
        /// </summary>
        public Bitmap original;

        /// <summary>
        /// Класс настроек
        /// </summary>
        public Settings settings = new Settings();

        
        private BaseNetwork network;
        private DatasetProcessor dataset;
        public MagicEye(BaseNetwork network, DatasetProcessor dataset)
        {
            this.network = network;
            this.dataset = dataset;
        }
        public MagicEye()
        {
        }
        public MagicEye(DatasetProcessor dataset)
        {
            this.dataset = dataset;
        }

        public bool ProcessImage(Bitmap bitmap)
        {
            // На вход поступает необработанное изображение с веб-камеры

            //  Минимальная сторона изображения (обычно это высота)
            if (bitmap.Height > bitmap.Width)
                //throw new Exception("К такой забавной камере меня жизнь не готовила!");
                return false;
            //  Можно было, конечено, и не кидаться эксепшенами в истерике, но идите и купите себе нормальную камеру!
            int side = bitmap.Height;

            //  Отпиливаем границы, но не более половины изображения
            if (side < 4 * settings.border) settings.border = side / 4;
            side -= 2 * settings.border;

            //  Мы сейчас занимаемся тем, что красиво оформляем входной кадр, чтобы вывести его на форму
            // Rectangle cropRect = new Rectangle((bitmap.Width - bitmap.Height) / 2 + settings.left + settings.border, settings.top + settings.border, side, side);
            Rectangle cropRect = new Rectangle(settings.border, settings.border, bitmap.Width - settings.border, bitmap.Height - settings.border);

            //  Тут создаём новый битмапчик, который будет исходным изображением
            original = new Bitmap(cropRect.Width, cropRect.Height);

            //  Объект для рисования создаём
            Graphics g = Graphics.FromImage(original);

            g.DrawImage(bitmap, new Rectangle(0, 0, original.Width, original.Height), cropRect, GraphicsUnit.Pixel);
            Pen p = new Pen(Color.Red);
            p.Width = 1;

            //  Теперь всю эту муть пилим в обработанное изображение
            AForge.Imaging.Filters.Grayscale grayFilter = new AForge.Imaging.Filters.Grayscale(0.2125, 0.7154, 0.0721);
            var uProcessed = grayFilter.Apply(AForge.Imaging.UnmanagedImage.FromManagedImage(original));


            int blockWidth = original.Width / settings.blocksCount;
            int blockHeight = original.Height / settings.blocksCount;
            for (int r = 0; r < settings.blocksCount; ++r)
                for (int c = 0; c < settings.blocksCount; ++c)
                {
                    //  Тут ещё обработку сделать
                    g.DrawRectangle(p, new Rectangle(c * blockWidth, r * blockHeight, blockWidth, blockHeight));
                }


            //  Масштабируем изображение до 500x500 - этого достаточно
            AForge.Imaging.Filters.ResizeBilinear scaleFilter = new AForge.Imaging.Filters.ResizeBilinear(settings.orignalDesiredSize.Width, settings.orignalDesiredSize.Height);
            uProcessed = scaleFilter.Apply(uProcessed);
            original = scaleFilter.Apply(original);
            g = Graphics.FromImage(original);
            //  Пороговый фильтр применяем. Величина порога берётся из настроек, и меняется на форме
            AForge.Imaging.Filters.BradleyLocalThresholding threshldFilter = new AForge.Imaging.Filters.BradleyLocalThresholding();
            threshldFilter.PixelBrightnessDifferenceLimit = settings.differenceLim;
            threshldFilter.ApplyInPlace(uProcessed);


            if (0==0)
            {

                string rez = "Обработка";
                ///  Инвертируем изображение
                AForge.Imaging.Filters.Invert InvertFilter = new AForge.Imaging.Filters.Invert();
                InvertFilter.ApplyInPlace(uProcessed);

                ///    Создаём BlobCounter, выдёргиваем самый большой кусок, масштабируем, пересечение и сохраняем
                ///    изображение в эксклюзивном использовании
                AForge.Imaging.BlobCounterBase bc = new AForge.Imaging.BlobCounter();

                bc.FilterBlobs = true;
                bc.MinWidth = 1;
                bc.MinHeight = 1;
                // Упорядочиваем по размеру
                bc.ObjectsOrder = AForge.Imaging.ObjectsOrder.Size;
                // Обрабатываем картинку

                bc.ProcessImage(uProcessed);

                Rectangle[] rects = bc.GetObjectsRectangles();
                rez = "Насчитали " + rects.Length.ToString() + " прямоугольников!";
                if (rects.Length > 10) 
                {
                    return false; 
                };
                // Частично уменьшаем границы прямоугольника для охвата только блобов
                int step = 400;
                if (rects.Length <= 1)
                    return false;

                Rectangle biggest_Rect = new Rectangle(rects[1].X - step / 2, rects[1].Y - step / 2, rects[1].Width + step, rects[1].Height + step);
                if (rects[0].Width != 500)
                {
                    biggest_Rect = new Rectangle(rects[0].X - step / 2, rects[0].Y - step / 2, rects[0].Width + step, rects[0].Height + step);
                }

                // Обрезаем изображение по прямоугольнику для удаления окружающей области
                AForge.Imaging.Filters.Crop cropFilter = new AForge.Imaging.Filters.Crop(biggest_Rect);
                uProcessed = cropFilter.Apply(uProcessed);

                // Масштабируем изображение до размера 14x14
                AForge.Imaging.Filters.ResizeBicubic scaleFilter2 = new AForge.Imaging.Filters.ResizeBicubic(300, 300);
                uProcessed = scaleFilter2.Apply(uProcessed);

                // Инвертируем изображение (необязательно, если это требуется в конкретном случае)
                InvertFilter.ApplyInPlace(uProcessed);


                Font f = new Font(FontFamily.GenericSansSerif, 20);
                g.DrawString(rez, f, Brushes.Black, 30, 30);
                currentType = processSample(uProcessed.ToManagedImage());
            }

            processed = uProcessed.ToManagedImage();

            return true;
        }

        public LetterType currentType { get; set; }

        /// <summary>
        /// Обработка одного сэмпла
        /// </summary>
        /// <param name="index"></param>
        private LetterType processSample(Bitmap bitmap)
        {
            Sample s = dataset.getSample(bitmap);
            return network.Predict(s);
        }

    }
}
