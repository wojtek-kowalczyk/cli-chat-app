using Common;

public class ClientGUI
{
    private Renderer renderer;

    public ClientGUI(string userName)
    {
        renderer = new Renderer(userName);
    }

    public void Invalidate(RoomState roomState)
    {
        renderer.Clear();
        renderer.Render(roomState.Messages, roomState.Users);
    }
}