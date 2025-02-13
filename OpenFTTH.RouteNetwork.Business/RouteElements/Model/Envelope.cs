using System;

namespace OpenFTTH.RouteNetwork.Business.RouteElements.Model
{
    public record class Envelope
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }


        public Envelope()
        {
            MinX = double.MaxValue;
            MinY = double.MaxValue;
            MaxX = double.MinValue;
            MaxY = double.MinValue;
        }

        public Envelope(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double Width
        {
            get { return MaxX - MinX; }
        }

        public double Height
        {
            get { return MaxY - MinY; }
        }

        public void ExpandToInclude(double x, double y)
        {
            if (MinX > x)
            { 
                MinX = x; 
            }

            if (MinY > y)
            {
                MinY = y;
            }

            if (MaxX < x)
            {
                MaxX = x;
            }

            if (MaxY < y)
            {
                MaxY = y;
            }
        }

        public bool IsWithin(double[] coordArray)
        {
            for (int i = 0; i < coordArray.Length; i+=2)
            {
                if (!IsWithin(coordArray[i], coordArray[i + 1]))
                    return false;
            }

            return true;
        }

        public bool IsWithin(double x, double y) 
        {
            if (x >= MinX && x <= MaxX && y >= MinY && y <= MaxY)
                return true;
            else
                return false;
        }

        public void Expand(double v)
        {
            MinX -= v;
            MinY -= v;
            MaxX += v;
            MaxY += v;
        }

        public void ExpandWidth(double v)
        {
            MinX -= v;
            MaxX += v;
        }

        public void ExpandHeight(double v)
        {
            MinY -= v;
            MaxY += v;
        }


        public void ExpandPercent(double percent)
        {
            if (percent <= 0)
            {
                throw new ApplicationException("Percent value must be greated than 0");
            }

            var extraMargin = (Width * percent) / 100 / 2;

            MinX -= extraMargin;
            MinY -= extraMargin;
            MaxX += extraMargin;
            MaxY += extraMargin;
        }
    }
}
