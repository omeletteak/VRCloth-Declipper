#!/usr/bin/env python3
"""採寸 JSONL を派生 SQLite 分析 DB に取り込む (docs/MEASUREMENT_SPEC.md §5).

生の jsonl が真実 (Unity の採寸 CLI が出力、No Cache のスカラーのみ)。この DB は
そこから再生成可能な分析キャッシュで、最近傍 / クラスタ / (将来) avatar×garment
行列を SQL の宣言的クエリで引くためのもの。SQLite 書き込みは Unity ツールに入れず
(Mono.Data.Sqlite 依存回避)、Python 標準ライブラリ sqlite3 だけで完結する。

使い方:
    python tools/measurements_to_sqlite.py <body-measurements.jsonl ...> \
        [-o out.sqlite] [--nearest "<avatar name>"]

DB はローカルの dev 成果物 (gitignore)。スキーマ vrcloth-body-measurement/1 を取り込む。
meshHash / conditions は将来のフィールド (今は NULL でも可、docs §6)。
"""
import argparse
import json
import math
import sqlite3


def ingest(con, paths):
    cur = con.cursor()
    cur.executescript(
        """
        CREATE TABLE IF NOT EXISTS avatars(
            name TEXT PRIMARY KEY,
            head_count_neck REAL, head_count_head REAL,
            height_m REAL, body_coverage REAL,
            mesh_hash TEXT, conditions_json TEXT,
            measured_at TEXT, schema TEXT);
        CREATE TABLE IF NOT EXISTS capsule_measurements(
            avatar TEXT, label TEXT,
            radius_m REAL, length_m REAL,
            sample_count INTEGER, estimated INTEGER,
            PRIMARY KEY(avatar, label),
            FOREIGN KEY(avatar) REFERENCES avatars(name));
        """
    )
    n = 0
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line:
                    continue
                d = json.loads(line)
                name = d["avatar"]
                conditions = d.get("conditions")
                cur.execute(
                    "INSERT OR REPLACE INTO avatars VALUES(?,?,?,?,?,?,?,?,?)",
                    (name, d.get("headCount_neckRef"), d.get("headCount_headRef"),
                     d.get("height_m"), d.get("bodyCoverage"),
                     d.get("meshHash"),
                     json.dumps(conditions) if conditions is not None else None,
                     d.get("timestamp"), d.get("schema")),
                )
                cur.execute("DELETE FROM capsule_measurements WHERE avatar=?", (name,))
                for c in d.get("capsules", []):
                    cur.execute(
                        "INSERT OR REPLACE INTO capsule_measurements VALUES(?,?,?,?,?,?)",
                        (name, c["label"], c["radius_m"], c["length_m"],
                         c.get("sampleCount", 0), 1 if c.get("estimated") else 0),
                    )
                n += 1
    con.commit()
    return n


def _radii(cur, name):
    return {label: r for label, r in cur.execute(
        "SELECT label, radius_m FROM capsule_measurements WHERE avatar=?", (name,))}


def _l2(values):
    norm = math.sqrt(sum(x * x for x in values))
    return [x / norm for x in values] if norm > 0 else values


def nearest(con, target):
    """Rank avatars by shape distance to `target` (docs/FAMILY_MODEL.md §4).

    Distance = Euclidean over L2-normalized radius vectors (overall scale removed),
    on the capsule labels the two share. Lower = closer body shape.
    """
    cur = con.cursor()
    tv = _radii(cur, target)
    if not tv:
        print(f"no measurements for '{target}'")
        return
    # Materialize the name list before the loop: _radii() reuses this cursor, which
    # would otherwise clobber a live outer iteration and truncate the ranking.
    names = [row[0] for row in cur.execute("SELECT name FROM avatars").fetchall()]
    rows = []
    for a in names:
        if a == target:
            continue
        av = _radii(cur, a)
        common = sorted(set(tv) & set(av))
        if not common:
            continue
        un = _l2([tv[l] for l in common])
        an = _l2([av[l] for l in common])
        dist = math.sqrt(sum((x - y) ** 2 for x, y in zip(un, an)))
        rows.append((dist, a))
    rows.sort()
    print(f"\nnearest to '{target}' (shape distance, scale-removed):")
    for dist, a in rows:
        print(f"  {dist:.4f}  {a}")


def main():
    ap = argparse.ArgumentParser(description="Ingest body-measurement JSONL into a SQLite analysis DB.")
    ap.add_argument("jsonl", nargs="+", help="body-measurement .jsonl file(s)")
    ap.add_argument("-o", "--db", default="vrcloth-measurements.sqlite", help="output SQLite DB (default: ./vrcloth-measurements.sqlite)")
    ap.add_argument("--nearest", metavar="AVATAR", help="after ingest, rank avatars by shape distance to this one")
    args = ap.parse_args()

    con = sqlite3.connect(args.db)
    try:
        n = ingest(con, args.jsonl)
        print(f"ingested {n} measurement row(s) -> {args.db}")
        if args.nearest:
            nearest(con, args.nearest)
    finally:
        con.close()


if __name__ == "__main__":
    main()
