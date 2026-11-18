namespace ColecaoMidias.Services;

/// <summary>
/// Atualiza a lista mostrada na tela (uma ObservableCollection) para refletir o que o filtro quer,
/// mexendo só no que mudou e podendo parar no meio para continuar depois (a interface respira entre os lotes).
/// A lista completa e a lista mostrada precisam estar na mesma ordem.
/// </summary>
public static class ListSync
{
    /// <summary>Tira da lista mostrada o que não existe mais na lista completa (por exemplo, a versão antiga de um álbum editado).</summary>
    public static void Purge<T>(IList<T> shown, IReadOnlyList<T> all) where T : class
    {
        if (shown.Count == 0)
            return;

        var valid = new HashSet<T>(all, ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        for (var i = shown.Count - 1; i >= 0; i--)
            if (!valid.Contains(shown[i]))
                shown.RemoveAt(i);
    }

    /// <summary>
    /// Um passo da atualização. Remove o que não deve aparecer e insere o que falta, no máximo
    /// <paramref name="maxInserts"/> itens por vez (criar cartões é a parte cara). Devolve true quando terminou.
    /// Para continuar, chame de novo com o mesmo <paramref name="index"/> e <paramref name="position"/>.
    /// </summary>
    public static bool Step<T>(IList<T> shown, IReadOnlyList<T> all, Func<T, bool> wanted,
                               ref int index, ref int position, int maxInserts) where T : class
    {
        var inserts = 0;
        for (; index < all.Count; index++)
        {
            var item = all[index];
            var want = wanted(item);
            var isShown = position < shown.Count && ReferenceEquals(shown[position], item);

            if (isShown)
            {
                if (want)
                    position++;
                else
                    shown.RemoveAt(position);
            }
            else if (want)
            {
                if (inserts >= maxInserts)
                    return false;   // continua daqui no próximo passo

                shown.Insert(position, item);
                position++;
                inserts++;
            }
        }

        // Sobras no fim não pertencem mais ao resultado.
        while (shown.Count > position)
            shown.RemoveAt(shown.Count - 1);

        return true;
    }
}
