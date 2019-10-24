using System;

namespace TurtleGraphicsCode
{

    public class Code
    {

        /// <summary>
        /// This is the place to put your turtle code
        /// </summary>
        public Turtle ToExecute()
        {

            Turtle t = new Turtle();
            t.Rotate(-90);

            for (int i = 0; i < 6; i++)
            {
                t.StoreTurtlePosition();


                for (int j = 0; i < 20; i++)
                {
                    DrawSection(t, 100);

                }


                t.RestoreTurtlePosition();
                t.Rotate(-90);
            }
            return t;
        }

        void DrawSection(Turtle t, int p)
        {
            t.Forward(10);
            t.StoreTurtlePosition();
            t.Rotate(-45);
            t.Forward(p);
            t.RestoreTurtlePosition();
            t.Rotate(45);
            t.Forward(p);
            t.RestoreTurtlePosition(true);
        }
    }
}
