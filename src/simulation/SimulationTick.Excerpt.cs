// 검토용 재구성: Assets/Project/Scripts/Core/Simulation/Simulation.cs
// 원본의 정책 틱과 Tick/PostTick 메서드 본문을 유지하고 클래스명과 필드만 정리했습니다.

public sealed class SimulationTickExcerpt
{
    private bool useFixedTick = true;
    private float fixedDt = 0.1f;
    private float simulationSpeed = 1f;
    private float timer;
    private bool isRunning;
    private float simulationTime;
    private int lastEmittedSecond;

    void Update()
    {
        if (!isRunning) return;

        float dt = Time.deltaTime * simulationSpeed;

        if (useFixedTick)
        {
            timer += dt;
            if (timer >= fixedDt)
            {
                Tick(fixedDt);
                timer -= fixedDt;
            }
        }
        else
        {
            Tick(dt);
        }
    }

    void Tick(float dt)
    {
        simulationTime += dt;

        int currentSecond = (int)simulationTime;
        if (currentSecond != lastEmittedSecond)
        {
            EmitTimeChanged();
            lastEmittedSecond = currentSecond;
        }

        SimulationEventBus.RaiseTick(dt);
        SimulationEventBus.RaisePostTick(dt);
    }

    // EmitTimeChanged와 시뮬레이션 제어 API는 생략했습니다.
}
