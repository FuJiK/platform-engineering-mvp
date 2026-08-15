(ns platform-mvp.policy)

(def supported-operations
  #{:get-status :get-logs :restart-service})

(defn ensure!
  [pred message data]
  (when-not pred
    (throw (ex-info message data))))

(defn validate-domain!
  [domain]
  (ensure! (map? domain)
           "Domain definition must be a map."
           {:value domain})

  (ensure! (keyword? (:service domain))
           ":service must be a keyword."
           {:service (:service domain)})

  (ensure! (keyword? (:environment domain))
           ":environment must be a keyword."
           {:environment (:environment domain)})

  (let [{:keys [image host-port container-port]} (:container domain)]
    (ensure! (and (string? image) (not-empty image))
             ":container :image must be a non-empty string."
             {:image image})
    (ensure! (and (integer? host-port) (<= 1 host-port 65535))
             ":container :host-port must be an integer from 1 to 65535."
             {:host-port host-port})
    (ensure! (and (integer? container-port) (<= 1 container-port 65535))
             ":container :container-port must be an integer from 1 to 65535."
             {:container-port container-port}))

  (let [operations (:operations domain)
        configured (set (keys operations))
        unsupported (seq (remove supported-operations configured))]
    (ensure! (map? operations)
             ":operations must be a map."
             {:operations operations})
    (ensure! (nil? unsupported)
             "Unsupported operation found. Dangerous operations must not be exposed by this MVP."
             {:unsupported unsupported
              :supported supported-operations})

    (doseq [[op {:keys [roles approval max-lines] :as cfg}] operations]
      (ensure! (and (set? roles) (seq roles) (every? keyword? roles))
               "Each operation must define a non-empty keyword role set."
               {:operation op :config cfg})

      (when (= op :restart-service)
        (ensure! (= :required approval)
                 "restart-service must require approval in this MVP."
                 {:operation op :approval approval}))

      (when (= op :get-logs)
        (ensure! (and (integer? max-lines) (<= 1 max-lines 1000))
                 "get-logs :max-lines must be an integer from 1 to 1000."
                 {:operation op :max-lines max-lines}))))

  domain)

(defn role-name
  [role]
  (if (keyword? role) (name role) (str role)))

(defn role-allowed?
  [ops-policy role operation]
  (let [requested (role-name role)
        allowed-roles (get-in ops-policy [:operations operation :roles] [])]
    (some #(= (role-name %) requested) allowed-roles)))
