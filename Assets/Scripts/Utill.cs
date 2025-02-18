using UnityEngine;

public class Utill
{
    /// <summary>
    /// Converte coordenadas centradas (0,0 no meio da imagem) 
    /// para um sistema 0..width / 0..height com origem no canto inferior esquerdo.
    /// </summary>
    public static Vector2 CenterToBottomLeft(Vector2 centerCoords, float imageWidth, float imageHeight)
    {
        float halfW = imageWidth / 2f;
        float halfH = imageHeight / 2f;

        // Se centerCoords for (-200, -100), ao somar + (320, 240) num quadro 640x480,
        // teremos (120, 140), ou seja, dentro de [0..640, 0..480].
        float x = centerCoords.x + halfW;
        float y = centerCoords.y + halfH;

        return new Vector2(x, y);
    }

    /// <summary>
    /// Escala coordenadas 0..imageWidth, 0..imageHeight para 0..Screen.width, 0..Screen.height.
    /// Útil quando o Canvas é Screen Space - Overlay e a imagem ocupa a tela inteira.
    /// </summary>
    public static Vector2 ScaleToScreenSpace(Vector2 bottomLeftCoords, float imageWidth, float imageHeight)
    {
        float scaledX = (bottomLeftCoords.x / imageWidth) * Screen.width;
        float scaledY = (bottomLeftCoords.y / imageHeight) * Screen.height;
        return new Vector2(scaledX, scaledY);
    }
}
