const activity = db.getSiblingDB("activity");
activity.createCollection("days");
activity.createCollection("settings");
const access = {
  sync: {days: ["find", "insert", "update"], settings: ["find"]},
  connector: {days: ["find"]},
  admin: {days: ["find"], settings: ["find", "insert", "update"]}
};
for (const [name, collections] of Object.entries(access)) {
  const role = "healthsyncy_" + name;
  const privileges = Object.entries(collections).map(([collection, actions]) => ({
    resource: {db: "activity", collection}, actions
  }));
  activity.createRole({role, privileges, roles: []});
  activity.createUser({user: name, pwd: process.env["MONGO_" + name.toUpperCase()], roles: [role]});
}
