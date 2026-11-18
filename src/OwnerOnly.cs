using System.Reflection;
using LabApi.Features.Wrappers;
using Mirror;

namespace SCPGrade
{
    /// <summary>
    /// Спавн объекта, видимого только одному игроку.
    /// Visibility.ForceHidden — сервер никому не рассылает объект (и новым игрокам тоже, SL CustomInterestManagement это уважает),
    /// затем вручную добавляем владельца в наблюдатели: ему приходит спавн и все дальнейшие изменения, остальным — ничего.
    /// </summary>
    internal static class OwnerOnly
    {
        private static readonly MethodInfo AddObserver =
            typeof(NetworkIdentity).GetMethod("AddObserver", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static void Spawn(AdminToy toy, Player owner)
        {
            NetworkIdentity id = toy.Base.netIdentity;
            id.visible = Visibility.ForceHidden;
            toy.Spawn();

            NetworkConnectionToClient conn = owner?.ConnectionToClient;
            if (conn != null && conn.isReady && AddObserver != null)
                AddObserver.Invoke(id, new object[] { conn });
        }
    }
}
