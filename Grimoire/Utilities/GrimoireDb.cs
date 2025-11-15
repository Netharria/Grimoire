// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace Grimoire.Utilities;

public record GrimoireDbEnv(IDbContextFactory<GrimoireDbContext> DbContextFactory);

public record GrimoireDb<A>(StateT<GrimoireDbEnv, IO, A> runDb) : K<GrimoireDb, A>
{
    public IO<(A Value, GrimoireDbEnv Env)> Run(GrimoireDbEnv env) =>
        runDb.Run(env).As();

    public static GrimoireDb<A> LiftIO(IO<A> ma) =>
        new(StateT<GrimoireDbEnv, IO, A>.LiftIO(ma));

    public static GrimoireDb<A> operator |(GrimoireDb<A> ma, GrimoireDb<A> mb) =>
        ma.Choose(mb).As();

    public GrimoireDb<B> Map<B>(Func<A, B> f) =>
        this.Kind().Map(f).As();

    public K<GrimoireDb, B> MapIO<B>(Func<IO<A>, IO<B>> f) =>
        this.Kind().MapIO(f).As();

    public GrimoireDb<B> Bind<B>(Func<A, GrimoireDb<B>> f) =>
        this.Kind().Bind(f).As();

    public GrimoireDb<C> SelectMany<B, C>(Func<A, GrimoireDb<B>> bind, Func<A, B, C> project) =>
        this.Kind().SelectMany(bind, project).As();

    public GrimoireDb<C> SelectMany<B, C>(Func<A, IO<B>> bind, Func<A, B, C> project) =>
        this.Kind().SelectMany(bind, project).As();

    public static implicit operator GrimoireDb<A>(Pure<A> ma) =>
        GrimoireDb.pure(ma.Value).As();

    public static implicit operator GrimoireDb<A>(Fail<Error> ma) =>
        GrimoireDb.fail<A>(ma.Value).As();

    public static implicit operator GrimoireDb<A>(Fail<string> ma) =>
        GrimoireDb.fail<A>(ma.Value);

    public static implicit operator GrimoireDb<A>(Error ma) =>
        GrimoireDb.fail<A>(ma).As();

}

public static class GrimoireDbExtensions
{
    public static GrimoireDb<A> As<A>(this K<GrimoireDb, A> ma) =>
    (GrimoireDb<A>)ma;
}

public class GrimoireDb :
    Deriving.Monad<GrimoireDb, StateT<GrimoireDbEnv, IO>>,
    Deriving.Stateful<GrimoireDb, StateT<GrimoireDbEnv, IO>, GrimoireDbEnv>,
    Deriving.Choice<GrimoireDb, StateT<GrimoireDbEnv, IO>>,
    Fallible<GrimoireDb>,
    MonadUnliftIO<GrimoireDb>
{
    static K<StateT<GrimoireDbEnv, IO>, A> Natural<GrimoireDb, StateT<GrimoireDbEnv, IO>>.Transform<A>(K<GrimoireDb, A> fa) =>
        fa.As().runDb;

    static K<GrimoireDb, A> CoNatural<GrimoireDb, StateT<GrimoireDbEnv, IO>>.CoTransform<A>(K<StateT<GrimoireDbEnv, IO>, A> fa) =>
        new GrimoireDb<A>(fa.As());

    static K<GrimoireDb, A> MonadIO<GrimoireDb>.LiftIO<A>(IO<A> ma) =>
        new GrimoireDb<A>(StateT<GrimoireDbEnv, IO, A>.LiftIO(ma));

    static K<GrimoireDb, IO<A>> MonadUnliftIO<GrimoireDb>.ToIO<A>(K<GrimoireDb, A> ma) =>
        dbEnv.Map(e => ma.As().Run(e).Map(vs => vs.Value));

    static K<GrimoireDb, B> MonadUnliftIO<GrimoireDb>.MapIO<A, B>(K<GrimoireDb, A> ma, Func<IO<A>, IO<B>> f) =>
        from e in dbEnv
        from r in f(ma.As().runDb.Run(e).Map(vs =>vs.Value).As())
        select r;

    static K<GrimoireDb, A> Fallible<Error, GrimoireDb>.Fail<A>(Error error) =>
     new GrimoireDb<A>(StateT.lift<GrimoireDbEnv, IO, A>(IO.fail<A>(error)));

    static K<GrimoireDb, A> Fallible<Error, GrimoireDb>.Catch<A>(K<GrimoireDb, A> ma, Func<Error, bool> Predicate, Func<Error, K<GrimoireDb, A>> Fail) =>
        from e in dbEnv
        from r in liftIO(ma.As().runDb.Run(e).Catch(Predicate, err => Fail(err).As().Run(e)).As())
        from _ in Stateful.put<GrimoireDb, GrimoireDbEnv>(r.Item2)
        select r.Value;

    public static GrimoireDb<A> liftIO<A>(IO<A> ma) =>
        MonadIO.liftIO<GrimoireDb, A>(ma).As();

    public static GrimoireDb<A> pure<A>(A value) =>
        Applicative.pure<GrimoireDb, A>(value).As();

    public static GrimoireDb<A> fail<A>(string error) =>
        Fallible.error<GrimoireDb, A>((Error)error).As();

    public static GrimoireDb<A> fail<A>(Error error) =>
        Fallible.error<GrimoireDb, A>(error).As();

    public static readonly GrimoireDb<GrimoireDbEnv> dbEnv =
        Stateful.get<GrimoireDb, GrimoireDbEnv>().As();
}
