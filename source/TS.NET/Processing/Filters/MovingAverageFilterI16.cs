namespace TS.NET;

public class MovingAverageFilterI16
{
    private readonly int[] buffer;
    private int index = 0;
    private int sum = 0;
    private readonly int points;

    public MovingAverageFilterI16(int points)
    {
        if (points < 2)
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be 2 or higher.");
        this.points = points;
        buffer = new int[points];
    }

    public void Process(Span<short> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            sum -= buffer[index];
            sum += data[i];
            buffer[index] = data[i];
            data[i] = (short)(sum / points);
            index++;
            if (index >= points)
                index = 0;
        }
    }
}
