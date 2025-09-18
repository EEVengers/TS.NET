namespace TS.NET;

public class MovingAverageFilterI8
{
    private readonly int[] buffer;
    private int index = 0;
    private int sum = 0;
    private readonly int points;

    public MovingAverageFilterI8(int points)
    {
        if (points < 2)
            throw new ArgumentOutOfRangeException(nameof(points), "Points must be greater than 2.");
        this.points = points;
        buffer = new int[points];
    }

    public void Process(Span<sbyte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            sum -= buffer[index];
            sum += data[i];
            buffer[index] = data[i];
            data[i] = (sbyte)(sum / points);
            index++;
            if (index >= points)
                index = 0;
        }
    }
}
