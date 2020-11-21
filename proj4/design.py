class Tweets:
    __init__(self):
        self.tweet_ID = 
        self.user_name = 
        self.text = "Retweet: aaa @Biden  #Biden  adsdad"
        self.is_re_tweet = True

class User:
    __init__(self):
        self.user_name = 
        self.password = 
        self.subscribe = set([user1, user2, user3, user4])

class DataBase:
    def __init__(self):
        self.tweets = set([Tweet1, tweet2, tweet3])
        self.users = {user_name: [Tweets]}
        self.hashtags = {hashtag: [Tweets], "#Biden": [tweet1, tweet2]}
        self.mention = {user_name: [Tweets], "@Biden":[tweet1, tweet2], "@Trump":[tweet7]}
